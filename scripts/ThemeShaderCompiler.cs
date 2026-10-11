using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CategoryBar.ThemeTools
{
    public static class ShaderCompiler
    {
        [ComImport, Guid("8BA5FB08-5195-40E2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IBlob
        {
            [PreserveSig] IntPtr GetBufferPointer();
            [PreserveSig] UIntPtr GetBufferSize();
        }
        [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi)]
        private static extern int D3DCompile(byte[] source, UIntPtr length, string sourceName, IntPtr defines, IntPtr include,
            string entry, string target, uint flags, uint effectFlags, out IBlob code, out IBlob errors);
        public static byte[] Compile(string source, string profile = "ps_2_0")
        {
            if (profile != "ps_2_0" && profile != "ps_3_0") throw new ArgumentException("WPF supports ps_2_0 or ps_3_0.");
            var bytes = Encoding.UTF8.GetBytes(source);
            IBlob code = null, errors = null;
            try
            {
                var result = D3DCompile(bytes, (UIntPtr)bytes.Length, "theme.hlsl", IntPtr.Zero, IntPtr.Zero, "main", profile, 0, 0, out code, out errors);
                if (result < 0) throw new InvalidDataException(errors == null ? "Shader compilation failed." : Marshal.PtrToStringAnsi(errors.GetBufferPointer()));
                var output = new byte[(int)code.GetBufferSize().ToUInt64()]; Marshal.Copy(code.GetBufferPointer(), output, 0, output.Length); return output;
            }
            finally
            {
                if (code != null) Marshal.ReleaseComObject(code);
                if (errors != null) Marshal.ReleaseComObject(errors);
            }
        }
    }
}
