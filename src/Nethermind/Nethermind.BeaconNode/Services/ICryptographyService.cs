﻿using System;
using System.Collections.Generic;
using Nethermind.BeaconNode.Containers;
using Nethermind.Core2.Crypto;
using Nethermind.Core2.Types;

namespace Nethermind.BeaconNode.Services
{
    public interface ICryptographyService
    {
        BlsPublicKey BlsAggregatePublicKeys(IEnumerable<BlsPublicKey> publicKeys);

        bool BlsVerify(BlsPublicKey publicKey, Hash32 signingRoot, BlsSignature signature, Domain domain);

        bool BlsVerifyMultiple(IEnumerable<BlsPublicKey> publicKeys, IEnumerable<Hash32> messageHashes, BlsSignature signature, Domain domain);

        Hash32 Hash(Hash32 a, Hash32 b);

        Hash32 Hash(ReadOnlySpan<byte> bytes);
    }
}
