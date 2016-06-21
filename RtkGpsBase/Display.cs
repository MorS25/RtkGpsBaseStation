﻿using System.Threading.Tasks;

namespace RtkGpsBase
{
    internal class Display
    {
        private static readonly SparkFunSerial16X2Lcd Lcd = new SparkFunSerial16X2Lcd();

        internal async Task Start()
        {
            await Lcd.Start();
        }

        internal static async Task Write(string text, int line)
        {
            if (line == 1)
                await Lcd.WriteToFirstLine(text);

            if (line == 2)
                await Lcd.WriteToSecondLine(text);
        }

        internal static async Task Write(string text)
        {
            await Lcd.Write(text);
        }
    }
}