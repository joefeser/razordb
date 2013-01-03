﻿/* 
Copyright 2012 Gnoso Inc.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using RazorDB;
using System.IO;

namespace RazorDBTests {

    [TestFixture]
    public class JournalTests {

        [Test]
        public void ReadAndWriteJournalFile() {

            string path = Path.GetFullPath("TestData\\RWJournal");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            JournalWriter jw = new JournalWriter(path, 324, false);

            List<KeyValuePair<KeyEx, Value>> items = new List<KeyValuePair<KeyEx, Value>>();
            for (int i = 0; i < 10000; i++) {
                KeyEx randKey = KeyEx.Random(20);
                Value randValue = Value.Random(100);
                jw.Add(randKey, randValue);
                items.Add(new KeyValuePair<KeyEx, Value>(randKey, randValue));
            }
            jw.Close();

            JournalReader jr = new JournalReader(path, 324);
            int j=0;
            foreach (var pair in jr.Enumerate()) {
                Assert.AreEqual( items[j].Key, pair.Key );
                Assert.AreEqual( items[j].Value, pair.Value );
                j++;
            }
            jr.Close();
        }

        [Test]
        public void ReadCorruptedJournalFile() {

            string path = Path.GetFullPath("TestData\\ReadCorruptedJournal");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            JournalWriter jw = new JournalWriter(path, 324, false);

            List<KeyValuePair<Key, Value>> items = new List<KeyValuePair<Key, Value>>();
            for (int i = 0; i < 10; i++) {
                Key randKey = Key.Random(20);
                Value randValue = Value.Random(100);
                jw.Add(randKey, randValue);
                items.Add(new KeyValuePair<Key, Value>(randKey, randValue));
            }
            jw.Close();

            // Reopen the file and add a partial record
            var fileName = Config.JournalFile(path, 324);
            var writer = new BinaryWriter(new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.None, 1024, false));
            Key key = Key.Random(20);
            Value value = Value.Random(100);
            writer.Write7BitEncodedInt(key.Length);
            writer.Write(key.InternalBytes);
            writer.Write7BitEncodedInt(0);
            writer.Flush();
            writer.Close();

            JournalReader jr = new JournalReader(path, 324);
            int j = 0;
            foreach (var pair in jr.Enumerate()) {
                Assert.AreEqual(items[j].Key, pair.Key);
                Assert.AreEqual(items[j].Value, pair.Value);
                j++;
            }
            jr.Close();
        }

        [Test]
        public void ReadAndWriteJournalFileWithAppend() {

            string path = Path.GetFullPath("TestData\\RWJournalAppend");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            JournalWriter jw = new JournalWriter(path, 324, false);

            List<KeyValuePair<KeyEx, Value>> items = new List<KeyValuePair<KeyEx, Value>>();
            for (int i = 0; i < 5000; i++) {
                KeyEx randKey = KeyEx.Random(20);
                Value randValue = Value.Random(100);
                jw.Add(randKey, randValue);
                items.Add(new KeyValuePair<KeyEx, Value>(randKey, randValue));
            }
            jw.Close();

            // reopen the same log for append
            jw = new JournalWriter(path, 324, true);
            for (int i = 0; i < 5000; i++) {
                KeyEx randKey = KeyEx.Random(20);
                Value randValue = Value.Random(100);
                jw.Add(randKey, randValue);
                items.Add(new KeyValuePair<KeyEx, Value>(randKey, randValue));
            }
            jw.Close();

            JournalReader jr = new JournalReader(path, 324);
            int j = 0;
            foreach (var pair in jr.Enumerate()) {
                Assert.AreEqual(items[j].Key, pair.Key);
                Assert.AreEqual(items[j].Value, pair.Value);
                j++;
            }
            jr.Close();
        }


    }
}
