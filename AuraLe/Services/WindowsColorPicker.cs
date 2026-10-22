using System;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace AuraLe.Services;

/// <summary>
/// Windows 기본 색상 선택 대화상자(ChooseColor) 및 네온 HSL 변환 헬퍼
/// </summary>
public static class WindowsColorPicker
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct CHOOSECOLOR
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public int rgbResult;
        public IntPtr lpCustColors;
        public int Flags;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
    }

    private const int CC_RGBINIT = 0x00000001;
    private const int CC_FULLOPEN = 0x00000002;
    private const int CC_ANYCOLOR = 0x00000100;

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool ChooseColor(ref CHOOSECOLOR lpcc);

    // 사용자가 프로그램 실행 중 커스텀 색상 슬롯 16개를 유지할 수 있도록 정적 배열 선언
    private static readonly int[] CustomColors = new int[16];

    /// <summary>
    /// Windows 기본 색상 선택창을 띄워 색상을 선택받습니다.
    /// </summary>
    public static Color? ShowColorDialog(IntPtr ownerHandle, Color? initialColor = null)
    {
        int initRgb = 0;
        if (initialColor.HasValue)
        {
            var c = initialColor.Value;
            // COLORREF: 0x00bbggrr (Win32 포맷)
            initRgb = c.R | (c.G << 8) | (c.B << 16);
        }

        var gch = GCHandle.Alloc(CustomColors, GCHandleType.Pinned);
        try
        {
            var cc = new CHOOSECOLOR
            {
                lStructSize = Marshal.SizeOf<CHOOSECOLOR>(),
                hwndOwner = ownerHandle,
                hInstance = IntPtr.Zero,
                rgbResult = initRgb,
                lpCustColors = gch.AddrOfPinnedObject(),
                Flags = CC_RGBINIT | CC_FULLOPEN | CC_ANYCOLOR
            };

            if (ChooseColor(ref cc))
            {
                int res = cc.rgbResult;
                byte r = (byte)(res & 0xFF);
                byte g = (byte)((res >> 8) & 0xFF);
                byte b = (byte)((res >> 16) & 0xFF);
                return Color.FromRgb(r, g, b);
            }
        }
        finally
        {
            gch.Free();
        }

        return null;
    }

    /// <summary>
    /// 어떤 색상이든 채도(Saturation)를 100%, 명도(Value)를 100%로 끌어올려 형광 네온 컬러로 변환합니다.
    /// </summary>
    public static Color ConvertToNeon(Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double h = 0;
        if (delta > 0.00001)
        {
            if (Math.Abs(max - r) < 0.00001) h = ((g - b) / delta) % 6;
            else if (Math.Abs(max - g) < 0.00001) h = ((b - r) / delta) + 2;
            else h = ((r - g) / delta) + 4;
            h *= 60;
            if (h < 0) h += 360;
        }
        else
        {
            // 무채색(흰색, 검은색, 회색 등)의 경우 기본 사이언(180도) 네온으로 대체
            h = 180;
        }

        // HSV: S = 1.0 (채도 100%), V = 1.0 (명도 100%)
        double c = 1.0;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));

        double r1 = 0, g1 = 0, b1 = 0;
        if (h < 60) { r1 = c; g1 = x; b1 = 0; }
        else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }

        return Color.FromRgb(
            (byte)Math.Clamp((int)Math.Round(r1 * 255), 0, 255),
            (byte)Math.Clamp((int)Math.Round(g1 * 255), 0, 255),
            (byte)Math.Clamp((int)Math.Round(b1 * 255), 0, 255)
        );
    }
}
