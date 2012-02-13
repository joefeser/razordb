﻿using System;
using NUnit.Framework;
using System.Text;
using RazorDB;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace RazorDBTests {

    [TestFixture]
    public class KeyValueStoreTests {

        [TestFixtureSetUp]
        public void Setup() {
            string path = Path.GetFullPath("TestData");
            if (!Directory.Exists(path)) 
                Directory.CreateDirectory(path);
        }

        [Test]
        public void BasicGetAndSet() {

            using (var db = new KeyValueStore("TestData\\GetAndSet")) {
                db.Truncate();

                for (int i = 0; i < 10; i++) {
                    byte[] key = BitConverter.GetBytes(i);
                    byte[] value = Encoding.UTF8.GetBytes("Number " + i.ToString());
                    db.Set(key, value);
                }

                for (int j = 0; j < 15; j++) {
                    byte[] key = BitConverter.GetBytes(j);

                    byte[] value = db.Get(key);
                    if (j < 10) {
                        Assert.AreEqual(Encoding.UTF8.GetBytes("Number " + j.ToString()), value);
                    } else {
                        Assert.IsNull(value);
                    }
                }
            }
        }

        [Test]
        public void BasicPersistentGetAndSet() {

            string path = Path.GetFullPath("TestData\\BasicPersistentGetAndSet");
            using (var db = new KeyValueStore(path)) {
                db.Truncate();

                for (int i = 0; i < 10; i++) {
                    byte[] key = BitConverter.GetBytes(i);
                    byte[] value = Encoding.UTF8.GetBytes("Number " + i.ToString());
                    db.Set(key, value);
                }
            }

            using (var db = new KeyValueStore(path)) {
                for (int j = 0; j < 15; j++) {
                    byte[] key = BitConverter.GetBytes(j);

                    byte[] value = db.Get(key);
                    if (j < 10) {
                        Assert.AreEqual(Encoding.UTF8.GetBytes("Number " + j.ToString()), value);
                    } else {
                        Assert.IsNull(value);
                    }
                }
            }
        }

        [Test]
        public void GetAndSetWithDelete() {

            string path = Path.GetFullPath("TestData\\GetAndSetWithDelete");
            using (var db = new KeyValueStore(path)) {
                db.Truncate();

                for (int i = 0; i < 10; i++) {
                    byte[] key = BitConverter.GetBytes(i);
                    byte[] value = Encoding.UTF8.GetBytes("Number " + i.ToString());
                    db.Set(key, value);
                }

                db.Delete(BitConverter.GetBytes(3));
                db.Delete(BitConverter.GetBytes(30));
                db.Delete(BitConverter.GetBytes(7));
                db.Delete(BitConverter.GetBytes(1));
                db.Delete(BitConverter.GetBytes(3));
            }

            using (var db = new KeyValueStore(path)) {
                for (int j = 0; j < 15; j++) {
                    byte[] key = BitConverter.GetBytes(j);

                    byte[] value = db.Get(key);
                    if (j == 3 || j == 1 || j == 7) {
                        Assert.IsNull(value);
                    } else if (j < 10) {
                        Assert.AreEqual(Encoding.UTF8.GetBytes("Number " + j.ToString()), value);
                    } else {
                        Assert.IsNull(value);
                    }
                }
            }
        }

        [Test]
        public void RotationShutdownRaceTest() {

            // Test to be sure that the rotation page has definitely been written by the time we exit the dispose region (the db must wait for that to occur).
            string path = Path.GetFullPath("TestData\\BulkSetWithDelete");
            using (var db = new KeyValueStore(path)) {
                db.Truncate();

                for (int i = 0; i < 100000; i++) {
                    byte[] key = BitConverter.GetBytes(i);
                    byte[] value = Encoding.UTF8.GetBytes("Number " + i.ToString());
                    db.Set(key, value);
                }
                // There is a chance that this could happen fast enough to make this assertion fail on some machines, but it should be unlikely.
                // The goal is to reproduce the race condition. If this assert succeeds then we have reproduced it.
                Assert.IsFalse(db.Manifest.GetPagesAtLevel(0).Length > 0);
            }
            using (var db = new KeyValueStore(path)) {
                Assert.IsTrue(db.Manifest.GetPagesAtLevel(0).Length > 0);
            }
        }

        [Test]
        public void RotationReadRaceTest() {

            string path = Path.GetFullPath("TestData\\BulkSetWithDelete");
            using (var db = new KeyValueStore(path)) {
                db.Truncate();

                for (int i = 0; i < 100000; i++) {
                    byte[] key = BitConverter.GetBytes(i);
                    byte[] value = Encoding.UTF8.GetBytes("Number " + i.ToString());
                    db.Set(key, value);
                }
                // Even though the page is rotated, but not written to disk yet, we should be able to query for the data anyway.
                {
                    byte[] key = BitConverter.GetBytes(0);
                    byte[] value = db.Get(key);
                    Assert.AreEqual(Encoding.UTF8.GetBytes("Number 0"), value);
                }
                // There is a chance that this could happen fast enough to make this assertion fail on some machines, but it should be unlikely.
                // The goal is to reproduce the race condition. If this assert succeeds then we have reproduced it.
                Assert.IsFalse(db.Manifest.GetPagesAtLevel(0).Length > 0);
            }
            using (var db = new KeyValueStore(path)) {
                Assert.IsTrue(db.Manifest.GetPagesAtLevel(0).Length > 0);
            }
        }


        [Test]
        public void BulkSetWithDelete() {

            int numItems = 1000000;
            string path = Path.GetFullPath("TestData\\BulkSetWithDelete");
            using (var db = new KeyValueStore(path)) {
                db.Manifest.Logger = msg => Console.WriteLine(msg);
                db.Truncate();

                for (int i = 0; i < numItems; i++) {
                    byte[] key = BitConverter.GetBytes(i);
                    byte[] value = Encoding.UTF8.GetBytes("Number " + i.ToString());
                    db.Set(key, value);
                }

                int skip = 10000;
                // Delete every skip-th item in reverse order,
                for (int j = numItems; j >= 0; j--) {
                    if (j % skip == 0) {
                        byte[] key = BitConverter.GetBytes(j);
                        db.Delete(key);
                    }
                }

                // Now check all the results
                for (int k = 0; k < numItems; k++) {
                    byte[] key = BitConverter.GetBytes(k);
                    byte[] value = db.Get(key);
                    if (k % skip == 0) {
                        Assert.IsNull(value);
                    } else {
                        Assert.AreEqual(Encoding.UTF8.GetBytes("Number " + k.ToString()), value, string.Format("{0}", k));
                    }
                }
            }
        }


        [Test]
        public void BulkSet() {

            string path = Path.GetFullPath("TestData\\BulkSet");
            var timer = new Stopwatch();
            int totalSize = 0;

            using (var db = new KeyValueStore(path)) {
                db.Truncate();

                db.Manifest.Logger = (msg) => { Console.WriteLine(msg); };

                timer.Start();
                for (int i = 0; i < 105000; i++) {
                    var randomKey = ByteArray.Random(40);
                    var randomValue = ByteArray.Random(256);
                    db.Set(randomKey.InternalBytes, randomValue.InternalBytes);

                    totalSize += randomKey.Length + randomValue.Length;
                }
                timer.Stop();
                Console.WriteLine("Wrote sorted table at a throughput of {0} MB/s", (double)totalSize / timer.Elapsed.TotalSeconds / (1024.0 * 1024.0));
            }

       }

        [Test]
        public void BulkThreadedSet() {

            int numThreads = 10;
            int totalItems = 100000;
            int totalSize = 0;

            string path = Path.GetFullPath("TestData\\BulkThreadedSet");

            List<Thread> threads = new List<Thread>();
            using (var db = new KeyValueStore(path)) {
                db.Truncate();

                for (int j = 0; j < numThreads; j++) {
                    threads.Add(new Thread( (num) => {
                        int itemsPerThread = totalItems / numThreads;
                        for (int i = 0; i < itemsPerThread; i++) {
                            var randomKey = new ByteArray( BitConverter.GetBytes( ((int)num * itemsPerThread) + i ) );
                            var randomValue = ByteArray.Random(256);
                            db.Set(randomKey.InternalBytes, randomValue.InternalBytes);

                            Interlocked.Add(ref totalSize, randomKey.Length + randomValue.Length);
                        }
                    }));
                }

                var timer = new Stopwatch();
                timer.Start();

                // Start all the threads
                int tnum = 0;
                threads.ForEach((t) => t.Start(tnum++));

                // Wait on all the threads to complete
                threads.ForEach((t) => t.Join(300000));

                timer.Stop();
                Console.WriteLine("Wrote sorted table at a throughput of {0} MB/s", (double)totalSize / timer.Elapsed.TotalSeconds / (1024.0 * 1024.0));
            }

        }

        [Test]
        public void BulkSetBulkGet() {

            string path = Path.GetFullPath("TestData\\BulkSetBulkGet");
            var timer = new Stopwatch();
            int totalSize = 0;
            int readSize = 0;

            var items = new Dictionary<ByteArray, ByteArray>();

            using (var db = new KeyValueStore(path)) {
                db.Truncate();

                db.Manifest.Logger = (msg) => Console.WriteLine(msg);

                timer.Start();
                for (int i = 0; i < 105000; i++) {
                    var randomKey = ByteArray.Random(40);
                    var randomValue = ByteArray.Random(256);
                    db.Set(randomKey.InternalBytes, randomValue.InternalBytes);

                    items[randomKey] = randomValue;
                    readSize += randomKey.Length + randomValue.Length;
                    totalSize += randomKey.Length + randomValue.Length;
                }
                timer.Stop();
                Console.WriteLine("Wrote sorted table at a throughput of {0} MB/s", (double)totalSize / timer.Elapsed.TotalSeconds / (1024.0 * 1024.0));
            }
            // Close and re-open the database to force all the sstable merging to complete.
            using (var db = new KeyValueStore(path)) {
                
                db.Manifest.Logger = (msg) => Console.WriteLine(msg);

                timer.Reset();
                Console.WriteLine("Begin randomized read back.");
                timer.Start();
                foreach ( var insertedItem in items) {
                    try {
                        byte[] value = db.Get(insertedItem.Key.InternalBytes);
                        Assert.AreEqual(insertedItem.Value, new ByteArray(value));
                    } catch (Exception /*e*/) {
                        //Console.WriteLine("Key: {0}\n{1}",insertedItem.Key,e);
                        //Debugger.Launch();
                        //db.Get(insertedItem.Key.InternalBytes);
                        //db.Manifest.LogContents();
                        throw;
                    }
                }
                timer.Stop();
                Console.WriteLine("Randomized read throughput of {0} MB/s (avg {1} ms per lookup)", (double)readSize / timer.Elapsed.TotalSeconds / (1024.0 * 1024.0), (double)timer.Elapsed.TotalSeconds / (double)items.Count);

            }

        }

        [Test]
        public void BulkSetGetWhileReMerging() {

            string path = Path.GetFullPath("TestData\\BulkSetGetWhileReMerging");
            var timer = new Stopwatch();
            int totalSize = 0;

            var items = new Dictionary<ByteArray, ByteArray>();

            using (var db = new KeyValueStore(path)) {
                db.Truncate();

                db.Manifest.Logger = (msg) => Console.WriteLine(msg);

                timer.Start();
                for (int i = 0; i < 105000; i++) {
                    var randomKey = ByteArray.Random(40);
                    var randomValue = ByteArray.Random(256);
                    db.Set(randomKey.InternalBytes, randomValue.InternalBytes);

                    items[randomKey] = randomValue;
                    totalSize += randomKey.Length + randomValue.Length;
                }
                timer.Stop();
                Console.WriteLine("Wrote sorted table at a throughput of {0} MB/s", (double)totalSize / timer.Elapsed.TotalSeconds / (1024.0 * 1024.0));

                timer.Reset();
                Console.WriteLine("Begin randomized read back.");
                timer.Start();
                foreach (var insertedItem in items) {
                    try {
                        byte[] value = db.Get(insertedItem.Key.InternalBytes);
                        Assert.AreEqual(insertedItem.Value, new ByteArray(value));
                    } catch (Exception /*e*/) {
                        //Console.WriteLine("Key: {0}\n{1}", insertedItem.Key, e);
                        //Debugger.Launch();
                        //db.Get(insertedItem.Key.InternalBytes);
                        //db.Manifest.LogContents();
                        throw;
                    }
                }
                timer.Stop();
                Console.WriteLine("Randomized read throughput of {0} MB/s (avg {1} ms per lookup)", (double)totalSize / timer.Elapsed.TotalSeconds / (1024.0 * 1024.0), (double)timer.Elapsed.TotalSeconds / (double)items.Count);

            }
        }

        [Test]
        public void BulkSetEnumerateAll() {

            string path = Path.GetFullPath("TestData\\BulkSetEnumerateAll");
            var timer = new Stopwatch();
            int totalSize = 0;
            int readSize = 0;

            using (var db = new KeyValueStore(path)) {
                db.Truncate();

                db.Manifest.Logger = (msg) => Console.WriteLine(msg);

                timer.Start();
                for (int i = 0; i < 105000; i++) {
                    var randomKey = BitConverter.GetBytes(i);
                    var randomValue = BitConverter.GetBytes(i);
                    db.Set(randomKey, randomValue);

                    readSize += randomKey.Length + randomValue.Length;
                    totalSize += randomKey.Length + randomValue.Length;
                }
                timer.Stop();
                Console.WriteLine("Wrote sorted table at a throughput of {0} MB/s", (double)totalSize / timer.Elapsed.TotalSeconds / (1024.0 * 1024.0));
            }
            // Close and re-open the database to force all the sstable merging to complete.
            using (var db = new KeyValueStore(path)) {

                db.Manifest.Logger = (msg) => Console.WriteLine(msg);

                timer.Reset();
                Console.WriteLine("Begin enumeration.");
                timer.Start();
                ByteArray lastKey = new ByteArray();
                int ct = 0;
                foreach (var pair in db.Enumerate()) {
                    try {
                        ByteArray k = new ByteArray(pair.Key);
                        ByteArray v = new ByteArray(pair.Value);
                        Assert.AreEqual(k, v);
                        Assert.True(lastKey.CompareTo(k) < 0);
                        lastKey = k;
                        ct++;
                    } catch (Exception /*e*/) {
                        //Console.WriteLine("Key: {0}\n{1}",insertedItem.Key,e);
                        //Debugger.Launch();
                        //db.Get(insertedItem.Key.InternalBytes);
                        //db.Manifest.LogContents();
                        throw;
                    }
                }
                timer.Stop();
                Assert.AreEqual(105000, ct, "105000 items should be enumerated.");

                Console.WriteLine("Enumerated read throughput of {0} MB/s (avg {1} ms per 1000 items)", (double)readSize / timer.Elapsed.TotalSeconds / (1024.0 * 1024.0), (double)timer.Elapsed.TotalSeconds / (double)105);
            }

        }

        [Test]
        public void BulkSetEnumerateFromKey() {

            string path = Path.GetFullPath("TestData\\BulkSetEnumerateFromKey");
            var timer = new Stopwatch();
            int totalSize = 0;
            int readSize = 0;

            using (var db = new KeyValueStore(path)) {
                db.Truncate();

                db.Manifest.Logger = (msg) => Console.WriteLine(msg);

                timer.Start();
                for (int i = 0; i < 105000; i++) {
                    var randomKey = BitConverter.GetBytes(i).Reverse().ToArray();
                    var randomValue = BitConverter.GetBytes(i);
                    db.Set(randomKey, randomValue);

                    readSize += randomKey.Length + randomValue.Length;
                    totalSize += randomKey.Length + randomValue.Length;
                }
                timer.Stop();
                Console.WriteLine("Wrote sorted table at a throughput of {0} MB/s", (double)totalSize / timer.Elapsed.TotalSeconds / (1024.0 * 1024.0));
            }
            // Close and re-open the database to force all the sstable merging to complete.
            using (var db = new KeyValueStore(path)) {

                db.Manifest.Logger = (msg) => Console.WriteLine(msg);

                timer.Reset();
                Console.WriteLine("Begin enumeration.");
                timer.Start();
                int lastKeyNum = 0;
                int ct = 0;
                int sum = 0;
                var searchKey = BitConverter.GetBytes(50000).Reverse().ToArray();
                foreach (var pair in db.EnumerateFromKey( searchKey )) {
                    try {
                        int num = BitConverter.ToInt32(pair.Key.Reverse().ToArray(),0);
                        ByteArray k = new ByteArray(pair.Key);
                        ByteArray v = new ByteArray(pair.Value);

                        Assert.GreaterOrEqual(num, 50000);
                        sum += num;
                        Assert.Less(lastKeyNum, num);
                        lastKeyNum = num;
                        ct++;
                    } catch (Exception /*e*/) {
                        //Console.WriteLine("Key: {0}\n{1}",insertedItem.Key,e);
                        //Debugger.Launch();
                        //db.Get(insertedItem.Key.InternalBytes);
                        //db.Manifest.LogContents();
                        throw;
                    }
                }
                timer.Stop();
                Assert.AreEqual(54999, ct, "55000 items should be enumerated.");

                Console.WriteLine("Enumerated read throughput of {0} MB/s (avg {1} ms per 1000 items)", (double)readSize / timer.Elapsed.TotalSeconds / (1024.0 * 1024.0), (double)timer.Elapsed.TotalSeconds / (double)105);
            }

        }

        [Test]
        public void BulkSetThreadedGetWhileReMerging() {

            string path = Path.GetFullPath("TestData\\BulkSetThreadedGetWhileReMerging");
            var timer = new Stopwatch();
            int totalSize = 0;

            var items = new Dictionary<ByteArray, ByteArray>();

            using (var db = new KeyValueStore(path)) {
                db.Truncate();

                db.Manifest.Logger = (msg) => Console.WriteLine(msg);

                timer.Start();
                int totalItems = 105000;
                for (int i = 0; i < totalItems; i++) {
                    var randomKey = ByteArray.Random(40);
                    var randomValue = ByteArray.Random(256);
                    db.Set(randomKey.InternalBytes, randomValue.InternalBytes);

                    items[randomKey] = randomValue;
                    totalSize += randomKey.Length + randomValue.Length;
                }
                timer.Stop();
                Console.WriteLine("Wrote sorted table at a throughput of {0} MB/s", (double)totalSize / timer.Elapsed.TotalSeconds / (1024.0 * 1024.0));

                List<KeyValuePair<ByteArray,ByteArray>> itemsList = items.ToList();
                int numThreads = 10;
                List<Thread> threads = new List<Thread>();

                for (int j = 0; j < numThreads; j++) {
                    threads.Add(new Thread((num) => {
                        int itemsPerThread = totalItems / numThreads;
                        for (int i = 0; i < itemsPerThread; i++) {
                            try {
                                int idx = i * (int)num;
                                byte[] value = db.Get(itemsList[idx].Key.InternalBytes);
                                Assert.AreEqual(itemsList[idx].Value, new ByteArray(value));
                            } catch (Exception /*e*/) {
                                //Console.WriteLine("Key: {0}\n{1}", insertedItem.Key, e);
                                //Debugger.Launch();
                                //db.Get(insertedItem.Key.InternalBytes);
                                //db.Manifest.LogContents();
                                throw;
                            }
                        }
                    }));
                }


                timer.Reset();
                Console.WriteLine("Begin randomized read back.");
                timer.Start();
                for (int k=0; k < numThreads; k++) {
                    threads[k].Start(k);
                }
                threads.ForEach(t => t.Join());
                timer.Stop();
                Console.WriteLine("Randomized read throughput of {0} MB/s (avg {1} ms per lookup)", (double)totalSize / timer.Elapsed.TotalSeconds / (1024.0 * 1024.0), (double)timer.Elapsed.TotalSeconds / (double)items.Count);

            }

        }

    }

}