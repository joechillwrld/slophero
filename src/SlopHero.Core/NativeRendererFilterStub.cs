namespace SlopHero.Core;

/// <summary>Builds the x64 bridge used by narrowly patched renderer callsites.</summary>
public static class NativeRendererFilterStub
{
    private const long FlagMarker = 0x1122334455667788;
    private const long TableMarker = 0x2233445566778899;
    private const long SetterMarker = 0x33445566778899AA;

    public static byte[] Build(IntPtr flag, IntPtr table, IntPtr setter)
    {
        var bytes = new List<byte>();
        var branches = new List<(int Operand, string Label)>();
        var labels = new Dictionary<string, int>();
        void Emit(params byte[] values) => bytes.AddRange(values);
        void Imm64(long marker) => Emit(BitConverter.GetBytes(marker));
        void Branch(byte opcode, string label)
        {
            Emit(opcode, 0);
            branches.Add((bytes.Count - 1, label));
        }

        Emit(0x49, 0xBA); Imm64(FlagMarker);       // mov r10, active flag
        Emit(0x41, 0x80, 0x3A, 0x00);             // cmp byte ptr [r10],0
        Branch(0x74, "miss");                     // je pass through
        Emit(0x49, 0x89, 0xCA);                     // mov r10,rcx
        Emit(0x49, 0xC1, 0xEA, 0x04);              // shr r10,4
        Emit(0x41, 0x81, 0xE2, 0xFF, 0x01, 0, 0); // and r10d,511
        Emit(0x49, 0xBB); Imm64(TableMarker);      // mov r11, pointer table
        labels["loop"] = bytes.Count;
        Emit(0x4B, 0x39, 0x0C, 0xD3);              // cmp [r11+r10*8],rcx
        Branch(0x74, "hit");
        Emit(0x4B, 0x83, 0x3C, 0xD3, 0x00);       // cmp qword ptr [..],0
        Branch(0x74, "miss");
        Emit(0x41, 0xFF, 0xC2);                    // inc r10d
        Emit(0x41, 0x81, 0xE2, 0xFF, 0x01, 0, 0); // and r10d,511
        Branch(0xEB, "loop");
        labels["hit"] = bytes.Count;
        Emit(0xC3);                                 // ret: target renderer is already hidden
        labels["miss"] = bytes.Count;
        Emit(0x49, 0xBB); Imm64(SetterMarker);      // mov r11, original Unity setter
        Emit(0x41, 0xFF, 0xE3);                    // jmp r11

        foreach (var branch in branches)
        {
            int delta = labels[branch.Label] - (branch.Operand + 1);
            if (delta < sbyte.MinValue || delta > sbyte.MaxValue)
                throw new InvalidOperationException("Native renderer filter branch exceeded short-jump range.");
            bytes[branch.Operand] = unchecked((byte)(sbyte)delta);
        }

        ReplaceAddress(bytes, FlagMarker, flag);
        ReplaceAddress(bytes, TableMarker, table);
        ReplaceAddress(bytes, SetterMarker, setter);
        return bytes.ToArray();
    }

    private static void ReplaceAddress(List<byte> bytes, long marker, IntPtr value)
    {
        byte[] token = BitConverter.GetBytes(marker);
        int match = -1;
        for (int i = 0; i <= bytes.Count - token.Length; i++)
        {
            bool equal = true;
            for (int j = 0; j < token.Length; j++)
                if (bytes[i + j] != token[j]) { equal = false; break; }
            if (!equal) continue;
            if (match >= 0) throw new InvalidOperationException("Ambiguous native renderer filter marker.");
            match = i;
        }
        if (match < 0) throw new InvalidOperationException("Native renderer filter marker is missing.");
        byte[] address = BitConverter.GetBytes(value.ToInt64());
        for (int i = 0; i < address.Length; i++) bytes[match + i] = address[i];
    }
}
