using CUE4Parse.Utils;
using System;
using System.Runtime.InteropServices;


namespace CUE4Parse.UE4.Objects.Core.Misc
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct FFrameNumber : IUStruct
    {
        public readonly int Value;

        public override string ToString() => Value.ToString();

        public float ToUnrealValue()
        {
            float unrealValue = ((float)Value / 6000 / 10);
            if (unrealValue < 0.001)
                return 0;
            var sdds = unrealValue.ToString();
            return unrealValue;
        }
    }
}
