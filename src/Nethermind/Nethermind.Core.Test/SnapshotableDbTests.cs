﻿/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

using Nethermind.Core.Crypto;
using Nethermind.Store;
using NUnit.Framework;

namespace Nethermind.Core.Test
{
    [TestFixture]
    public class SnapshotableDbTests
    {
        private Keccak _hash1 = Keccak.Compute("1");

        private readonly byte[] _bytes1 = new byte[] {1};
        private readonly byte[] _bytes2 = new byte[] {2};

        [Test]
        public void Set_get()
        {
            SnapshotableDb db = new SnapshotableDb(new MemDb());
            db.Set(_hash1, _bytes1);
            byte[] getResult = db.Get(_hash1);
            Assert.AreEqual(_bytes1, getResult);
        }
        
        [Test]
        public void Double_set_get()
        {
            SnapshotableDb db = new SnapshotableDb(new MemDb());
            db.Set(_hash1, _bytes1);
            db.Set(_hash1, _bytes2);
            byte[] getResult = db.Get(_hash1);
            Assert.AreEqual(_bytes2, getResult);
        }
        
        [Test]
        public void Initial_take_snapshot()
        {
            SnapshotableDb db = new SnapshotableDb(new MemDb());
            Assert.AreEqual(-1, db.TakeSnapshot());
        }
        
        [Test]
        public void Set_take_snapshot()
        {
            SnapshotableDb db = new SnapshotableDb(new MemDb());
            db.Set(_hash1, _bytes1);
            Assert.AreEqual(0, db.TakeSnapshot());
        }
        
        [Test]
        public void Set_restore_get()
        {
            SnapshotableDb db = new SnapshotableDb(new MemDb());
            db.Set(_hash1, _bytes1);
            db.Restore(-1);
            byte[] getResult = db.Get(_hash1);
            Assert.AreEqual(null, getResult);
        }
        
        [Test]
        public void Set_commit_get()
        {
            SnapshotableDb db = new SnapshotableDb(new MemDb());
            db.Set(_hash1, _bytes1);
            db.Commit();
            byte[] getResult = db.Get(_hash1);
            Assert.AreEqual(_bytes1, getResult);
        }
        
        [Test]
        public void Over_cache_capacity()
        {
            SnapshotableDb db = new SnapshotableDb(new MemDb(), true, 4);
            Keccak firstHash = Keccak.Compute(_hash1.Bytes);
            for (int i = 0; i < 8; i++)
            {
                _hash1 = Keccak.Compute(_hash1.Bytes);
                db.Set(_hash1, _bytes1);    
            }
            
            byte[] getResult = db.Get(firstHash);
            Assert.AreEqual(_bytes1, getResult);
        }
        
        [Test]
        public void Capacity_grwoth_and_shrinkage()
        {
            SnapshotableDb db = new SnapshotableDb(new MemDb());
            for (int i = 0; i < 16; i++)
            {
                _hash1 = Keccak.Compute(_hash1.Bytes); 
                db.Set(_hash1, _bytes1);
            }
            
            db.Restore(-1);
            
            byte[] getResult = db.Get(_hash1);
            Assert.AreEqual(null, getResult);
            
            for (int i = 0; i < 16; i++)
            {
                _hash1 = Keccak.Compute(_hash1.Bytes);
                db.Set(_hash1, _bytes1);
            }
            
            db.Commit();
            
            getResult = db.Get(_hash1);
            Assert.AreEqual(_bytes1, getResult);
        }
    }
}