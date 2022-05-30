using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronBinaryTemplate.LibraryFunction
{

    public class CRC32
    {
        private readonly uint[] ChecksumTable;
        private readonly uint Polynomial = 0xEDB88320;

        public CRC32()
        {
            ChecksumTable = new uint[0x100];

            for (uint index = 0; index < 0x100; ++index)
            {
                uint item = index;
                for (int bit = 0; bit < 8; ++bit)
                    item = ((item & 1) != 0) ? (Polynomial ^ (item >> 1)) : (item >> 1);
                ChecksumTable[index] = item;
            }
        }

        public uint ComputeHash(byte[] data)
        {
            uint result = 0xFFFFFFFF;

            for (int current = 0; current < data.Length; ++current)
                result = ChecksumTable[(result & 0xFF) ^ (byte)current] ^ (result >> 8);

            return ~result;
        }

    }
}