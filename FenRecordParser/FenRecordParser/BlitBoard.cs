using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FenRecordParser
{
    [StructLayout(LayoutKind.Sequential)]
    struct Blitboard
    {
        public ulong pawns;
        public ulong knights;
        public ulong bishops;
        public ulong rooks;
        public ulong queens;
        public ulong kings;
        public ulong white;
        public ulong black;
        public uint sideToMove;
        public uint castlingRights;
        public uint epTargetSquare;
        public uint halfmoveClock;
        public uint fullmoveNumber;
    }
}
