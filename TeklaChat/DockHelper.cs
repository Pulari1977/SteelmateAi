using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TeklaChat;

public static class DockHelper
{
	private struct RECT
	{
		public int L;

		public int T;

		public int R;

		public int B;
	}

	private const int SW_RESTORE = 9;

	private static Rectangle _teklaBefore;

	private static bool _teklaMoved;

	public const int PanelWidth = 460;

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr h, out RECT r);

	[DllImport("user32.dll")]
	private static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hgt, bool repaint);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr h, int cmd);

	[DllImport("user32.dll")]
	private static extern bool IsZoomed(IntPtr h);

	public static IntPtr FindTekla()
	{
		try
		{
			Process[] processesByName = Process.GetProcessesByName("TeklaStructures");
			foreach (Process process in processesByName)
			{
				if (process.MainWindowHandle != IntPtr.Zero)
				{
					return process.MainWindowHandle;
				}
			}
		}
		catch
		{
		}
		return IntPtr.Zero;
	}

	public static void Dock(Form f, bool right, bool fitTekla)
	{
		Rectangle workingArea = Screen.FromControl((Control)(object)f).WorkingArea;
		f.FormBorderStyle = (FormBorderStyle)6;
		((Control)f).MinimumSize = new Size(340, 480);
		f.StartPosition = (FormStartPosition)0;
		int x = (right ? (workingArea.Right - 460) : workingArea.Left);
		int height = Math.Min(workingArea.Height, Math.Max(520, (int)((double)workingArea.Height * 0.7)));
		((Control)f).Bounds = new Rectangle(x, workingArea.Top + 8, 460, height);
		f.TopMost = true;
		if (!fitTekla)
		{
			return;
		}
		IntPtr intPtr = FindTekla();
		if (!(intPtr == IntPtr.Zero))
		{
			if (!_teklaMoved && GetWindowRect(intPtr, out var r))
			{
				_teklaBefore = Rectangle.FromLTRB(r.L, r.T, r.R, r.B);
				_teklaMoved = true;
			}
			if (IsZoomed(intPtr))
			{
				ShowWindow(intPtr, 9);
			}
			MoveWindow(intPtr, right ? workingArea.Left : (workingArea.Left + 460), workingArea.Top, workingArea.Width - 460, workingArea.Height, repaint: true);
		}
	}

	public static void Undock(Form f)
	{
		f.TopMost = false;
		f.FormBorderStyle = (FormBorderStyle)4;
		((Control)f).MinimumSize = new Size(340, 480);
		f.Size = new Size(480, 760);
		f.StartPosition = (FormStartPosition)1;
		Rectangle workingArea = Screen.FromControl((Control)(object)f).WorkingArea;
		f.Location = new Point(workingArea.Left + (workingArea.Width - ((Control)f).Width) / 2, workingArea.Top + (workingArea.Height - ((Control)f).Height) / 2);
		IntPtr intPtr = FindTekla();
		if (_teklaMoved && intPtr != IntPtr.Zero)
		{
			MoveWindow(intPtr, _teklaBefore.Left, _teklaBefore.Top, _teklaBefore.Width, _teklaBefore.Height, repaint: true);
		}
		_teklaMoved = false;
	}
}
