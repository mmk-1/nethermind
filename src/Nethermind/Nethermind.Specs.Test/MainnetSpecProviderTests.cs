//  Copyright (c) 2018 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
// 

using FluentAssertions;
using Nethermind.Core.Specs;
using NUnit.Framework;

namespace Nethermind.Specs.Test
{
    [TestFixture]
    public class MainnetSpecProviderTests
    {
        private readonly ISpecProvider _specProvider = MainnetSpecProvider.Instance;
        
        [TestCase(12_243_999, false)]
        [TestCase(12_244_000, false)]
        public void Ropsten_berlin_eips(long blockNumber, bool isEnabled)
        {
            _specProvider.GetSpec(blockNumber).IsEip2315Enabled.Should().Be(isEnabled);
            _specProvider.GetSpec(blockNumber).IsEip2537Enabled.Should().Be(isEnabled);
            _specProvider.GetSpec(blockNumber).IsEip2565Enabled.Should().Be(isEnabled);
            _specProvider.GetSpec(blockNumber).IsEip2718Enabled.Should().Be(isEnabled);
            _specProvider.GetSpec(blockNumber).IsEip2929Enabled.Should().Be(isEnabled);
            _specProvider.GetSpec(blockNumber).IsEip2930Enabled.Should().Be(isEnabled);
        }
        
        [Test]
        public void Dao_block_number_is_correct()
        {
            _specProvider.DaoBlockNumber.Should().Be(1920000L);
        }
    }
}
