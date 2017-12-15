﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Nevermind.Core.Crypto;
using Nevermind.Core.Extensions;

namespace Nevermind.Core.Encoding
{
    /// <summary>
    /// https://github.com/ethereum/wiki/wiki/RLP
    /// </summary>
    [DebuggerStepThrough]
    public class Rlp : IEquatable<Rlp>
    {
        public static readonly Rlp OfEmptyByteArray = new Rlp(128);
        
        public static readonly Rlp OfEmptySequence = new Rlp(192);

        private static readonly Dictionary<RuntimeTypeHandle, IRlpDecoder> Decoders =
            new Dictionary<RuntimeTypeHandle, IRlpDecoder>
            {
                [typeof(Transaction).TypeHandle] = new TransactionDecoder(),
                [typeof(Account).TypeHandle] = new AccountDecoder(),
                [typeof(Block).TypeHandle] = new BlockDecoder(),
                [typeof(BlockHeader).TypeHandle] = new BlockHeaderDecoder()
            };

        public Rlp(byte singleByte)
        {
            Bytes = new[] {singleByte};
        }

        public Rlp(byte[] bytes)
        {
            Bytes = bytes;
        }

        public byte[] Bytes { get; }

        public byte this[int index] => Bytes[index];
        public int Length => Bytes.Length;

        public bool Equals(Rlp other)
        {
            if (other == null)
            {
                return false;
            }

            return Extensions.Bytes.UnsafeCompare(Bytes, other.Bytes);
        }

        public static object Decode(Rlp rlp)
        {
            return Decode(new DecoderContext(rlp.Bytes));
        }

        private static object Decode(DecoderContext context, bool check = true)
        {
            object CheckAndReturn(List<object> resultToCollapse, DecoderContext contextToCheck)
            {
                if (check && contextToCheck.CurrentIndex != contextToCheck.MaxIndex)
                {
                    throw new InvalidOperationException();
                }

                if (resultToCollapse.Count == 1)
                {
                    return resultToCollapse[0];
                }

                return resultToCollapse.ToArray();
            }

            List<object> result = new List<object>();

            byte prefix = context.Pop();

            if (prefix == 0)
            {
                result.Add(new byte[] {0});
                return CheckAndReturn(result, context);
            }

            if (prefix < 128)
            {
                result.Add(new[] {prefix});
                return CheckAndReturn(result, context);
            }

            if (prefix == 128)
            {
                result.Add(new byte[] { });
                return CheckAndReturn(result, context);
            }

            if (prefix <= 183)
            {
                int length = prefix - 128;
                byte[] data = context.Pop(length);
                if (data.Length == 1 && data[0] < 128)
                {
                    throw new InvalidOperationException();
                }

                result.Add(data);
                return CheckAndReturn(result, context);
            }

            if (prefix < 192)
            {
                int lengthOfLength = prefix - 183;
                if (lengthOfLength > 4)
                {
                    // strange but needed to pass tests -seems that spec gives int64 length and tests int32 length
                    throw new InvalidOperationException();
                }

                int length = DeserializeLength(context.Pop(lengthOfLength));
                if (length < 56)
                {
                    throw new InvalidOperationException();
                }

                byte[] data = context.Pop(length);
                result.Add(data);
                return CheckAndReturn(result, context);
            }

            int concatenationLength;
            if (prefix <= 247)
            {
                concatenationLength = prefix - 192;
            }
            else
            {
                int lengthOfConcatenationLength = prefix - 247;
                if (lengthOfConcatenationLength > 4)
                {
                    // strange but needed to pass tests -seems that spec gives int64 length and tests int32 length
                    throw new InvalidOperationException();
                }

                concatenationLength = DeserializeLength(context.Pop(lengthOfConcatenationLength));
                if (concatenationLength < 56)
                {
                    throw new InvalidOperationException();
                }
            }

            long startIndex = context.CurrentIndex;
            List<object> nestedList = new List<object>();
            while (context.CurrentIndex < startIndex + concatenationLength)
            {
                nestedList.Add(Decode(context, false));
            }

            result.Add(nestedList.ToArray());

            return CheckAndReturn(result, context);
        }

        public static int DeserializeLength(byte[] bytes)
        {
            const int size = sizeof(int);
            byte[] padded = new byte[size];
            Array.Copy(bytes, 0, padded, size - bytes.Length, bytes.Length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(padded);
            }

            return BitConverter.ToInt32(padded, 0);
        }

        public static Rlp Encode(params Keccak[] sequence)
        {
            Rlp[] rlpSequence = new Rlp[sequence.Length];
            for (int i = 0; i < sequence.Length; i++)
            {
                rlpSequence[i] = Encode(sequence[i]);
            }

            return Encode(rlpSequence);
        }

        public static Rlp Encode(params Rlp[] sequence)
        {
            int contentLength = 0;
            for (int i = 0; i < sequence.Length; i++)
            {
                contentLength += sequence[i].Length;
            }

            byte[] content = new byte[contentLength];
            int offset = 0;
            for (int i = 0; i < sequence.Length; i++)
            {
                Buffer.BlockCopy(sequence[i].Bytes, 0, content, offset, sequence[i].Length);
                offset += sequence[i].Length;
            }

            if (contentLength < 56)
            {
                return new Rlp(Extensions.Bytes.Concat((byte)(192 + contentLength), content));
            }

            byte[] serializedLength = SerializeLength(contentLength);
            byte prefix = (byte)(247 + serializedLength.Length);
            return new Rlp(Extensions.Bytes.Concat(prefix, serializedLength, content));
        }

        public static Rlp Encode(params object[] sequence)
        {
            Rlp[] rlpSequence = new Rlp[sequence.Length];
            for (int i = 0; i < sequence.Length; i++)
            {
                rlpSequence[i] = Encode(sequence[i]);
            }

            return Encode(rlpSequence);
        }

        public static Rlp Encode(BigInteger bigInteger)
        {
            return bigInteger == 0 ? OfEmptyByteArray : Encode(bigInteger.ToBigEndianByteArray());
        }

        public static Rlp Encode(object item)
        {
            switch (item)
            {
                case byte _:
                case short _:
                case int _:
                case ushort _:
                case uint _:
                case long _:
                    long value = (long)item;

                    // check test bytestring00 and zero - here is some inconsistency in tests
                    if (value == 0L)
                    {
                        return OfEmptyByteArray;
                    }

                    if (value < 128L)
                    {
                        // ReSharper disable once PossibleInvalidCastException
                        return new Rlp(Convert.ToByte(value));
                    }

                    if (value <= byte.MaxValue)
                    {
                        return Encode(new[] {Convert.ToByte(value)});
                    }

                    if (value <= short.MaxValue)
                    {
                        return Encode(((short)value).ToBigEndianByteArray());
                    }

                    return Encode(new BigInteger(value));
                case null:
                    return OfEmptyByteArray;
                case BigInteger bigInt:
                    return Encode(bigInt);
                case string s:
                    return Encode(s);
                case Rlp rlp:
                    return rlp;
                case ulong ulongNumber:
                    return Encode(ulongNumber.ToBigEndianByteArray());
                case object[] objects:
                    return Encode(objects);
                case byte[] byteArray:
                    return Encode(byteArray);
                case Keccak keccak:
                    return Encode(keccak);
                case Keccak[] keccakArray:
                    return Encode(keccakArray);
                case Address address:
                    return Encode(address);
                case LogEntry logEntry:
                    return Encode(logEntry);
                case Block block:
                    return Encode(block);
                case BlockHeader header:
                    return Encode(header);
                case Bloom bloom:
                    return Encode(bloom);
            }

            throw new NotSupportedException($"RLP does not support items of type {item.GetType().Name}");
        }

        public static Rlp Encode(string s)
        {
            return Encode(System.Text.Encoding.ASCII.GetBytes(s));
        }
        
        public static Rlp Encode(byte[] input)
        {
            if (input.Length == 0)
            {
                return OfEmptyByteArray;
            }

            if (input.Length == 1 && input[0] < 128)
            {
                return new Rlp(input[0]);
            }

            if (input.Length < 56)
            {
                byte smallPrefix = (byte)(input.Length + 128);
                return new Rlp(Extensions.Bytes.Concat(smallPrefix, input));
            }

            byte[] serializedLength = SerializeLength(input.Length);
            byte prefix = (byte)(183 + serializedLength.Length);
            return new Rlp(Extensions.Bytes.Concat(prefix, serializedLength, input));
        }

        public static byte[] SerializeLength(long value)
        {
            const int maxResultLength = 8;
            byte[] bytes = new byte[maxResultLength];

            bytes[0] = (byte)(value >> 56);
            bytes[1] = (byte)(value >> 48);
            bytes[2] = (byte)(value >> 40);
            bytes[3] = (byte)(value >> 32);
            bytes[4] = (byte)(value >> 24);
            bytes[5] = (byte)(value >> 16);
            bytes[6] = (byte)(value >> 8);
            bytes[7] = (byte)value;

            int resultLength = maxResultLength;
            for (int i = 0; i < maxResultLength; i++)
            {
                if (bytes[i] == 0)
                {
                    resultLength--;
                }
                else
                {
                    break;
                }
            }

            byte[] result = new byte[resultLength];
            Buffer.BlockCopy(bytes, maxResultLength - resultLength, result, 0, resultLength);
            return result;
        }

        public static Rlp Encode(BlockHeader header)
        {
            return Encode(
                header.ParentHash,
                header.OmmersHash,
                header.Beneficiary,
                header.StateRoot,
                header.TransactionsRoot,
                header.ReceiptsRoot,
                header.Bloom,
                header.Difficulty,
                header.Number,
                header.GasLimit,
                header.GasUsed,
                header.Timestamp,
                header.ExtraData,
                header.MixHash,
                header.Nonce
            );
        }

        public static Rlp Encode(Block block)
        {
            return Encode(block.Header, block.Transactions, block.Ommers);
        }

        public static Rlp Encode(Bloom bloom)
        {
            byte[] result = new byte[259];
            result[0] = 185;
            result[1] = 1;
            result[2] = 0;
            Buffer.BlockCopy(bloom.Bytes, 0, result, 3, 256);
            return new Rlp(result);
        }

        public static Rlp Encode(LogEntry logEntry)
        {
            // TODO: can be slightly optimized in place
            return Encode(
                logEntry.LoggersAddress,
                logEntry.Topics,
                logEntry.Data);
        }

        public static Rlp Encode(Account account)
        {
            return Encode(
                account.Nonce,
                account.Balance,
                account.StorageRoot,
                account.CodeHash);
        }

        public static Rlp Encode(TransactionReceipt receipt, bool isEip658Enabled)
        {
            if (isEip658Enabled)
            {
                return Encode(
                    receipt.StatusCode,
                    receipt.GasUsed,
                    receipt.Bloom,
                    receipt.Logs);
            }

            return Encode(
                receipt.PostTransactionState,
                receipt.GasUsed,
                receipt.Bloom,
                receipt.Logs);
        }

        public static T Decode<T>(Rlp rlp)
        {
            if (Decoders.ContainsKey(typeof(T).TypeHandle))
            {
                return ((IRlpDecoder<T>)Decoders[typeof(T).TypeHandle]).Decode(rlp);
            }

            throw new NotImplementedException();
        }

        public static Rlp Encode(Transaction transaction, bool forSigning, bool eip155 = false, int chainId = 0)
        {
            object[] sequence = new object[forSigning && !eip155 ? 6 : 9];
            sequence[0] = transaction.Nonce;
            sequence[1] = transaction.GasPrice;
            sequence[2] = transaction.GasLimit;
            sequence[3] = transaction.To;
            sequence[4] = transaction.Value;
            sequence[5] = transaction.To == null ? transaction.Init : transaction.Data;

            if (forSigning)
            {
                if (eip155)
                {
                    sequence[6] = chainId;
                    sequence[7] = BigInteger.Zero;
                    sequence[8] = BigInteger.Zero;
                }
            }
            else
            {
                sequence[6] = transaction.Signature?.V;
                sequence[7] = transaction.Signature?.R.WithoutLeadingZeros(); // TODO: consider storing R and S differently
                sequence[8] = transaction.Signature?.S.WithoutLeadingZeros(); // TODO: consider storing R and S differently
            }

            return Encode(sequence);
        }

        public static Rlp Encode(Transaction transaction)
        {
            return Encode(transaction, false);
        }

        public static Rlp Encode(Keccak keccak)
        {
            byte[] result = new byte[33];
            result[0] = 160;
            Buffer.BlockCopy(keccak.Bytes, 0, result, 1, 32);
            return new Rlp(result);
        }

        public static Keccak DecodeKeccak(Rlp rlp)
        {
            return new Keccak(rlp.Bytes.Slice(1, 32));
        }

        public static Rlp Encode(Address address)
        {
            if (address == null)
            {
                return OfEmptyByteArray;
            }

            byte[] result = new byte[21];
            result[0] = 148;
            Buffer.BlockCopy(address.Hex, 0, result, 1, 20);
            return new Rlp(result);
        }

        public string ToString(bool withZeroX)
        {
            return Hex.FromBytes(Bytes, withZeroX);
        }

        public override string ToString()
        {
            return ToString(true);
        }

        public int GetHashCode(Rlp obj)
        {
            return obj.Bytes.GetXxHashCode();
        }

        public class DecoderContext
        {
            public DecoderContext(byte[] data)
            {
                Data = data;
                MaxIndex = Data.Length;
            }

            public byte[] Data { get; }
            public int CurrentIndex { get; set; }
            public int MaxIndex { get; set; }

            public byte Pop()
            {
                return Data[CurrentIndex++];
            }

            public byte[] Pop(int n)
            {
                byte[] bytes = new byte[n];
                Buffer.BlockCopy(Data, CurrentIndex, bytes, 0, n);
                CurrentIndex += n;
                return bytes;
            }
        }
    }
}