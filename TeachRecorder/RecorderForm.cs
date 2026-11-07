using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TeachRecorder;

public class RecorderForm : Form
{
	private const int VK_LBUTTON = 1;

	private const int VK_RBUTTON = 2;

	private const int VK_MBUTTON = 4;

	private const int MaxMinutes = 30;

	private const int MaxFrames = 2000;

	private const int MaxWidth = 1600;

	private const long JpegQuality = 70L;

	private const double ChangeThreshold = 2.0;

	private readonly Button _btn = new Button();

	private readonly Label _status = new Label();

	private readonly TextBox _note = new TextBox();

	private readonly NumericUpDown _interval = new NumericUpDown();

	private readonly Timer _frameTimer = new Timer();

	private readonly Timer _clickTimer = new Timer
	{
		Interval = 40
	};

	private bool _recording;

	private string _dir;

	private StreamWriter _log;

	private Stopwatch _clock;

	private int _frames;

	private int _skipped;

	private double[] _lastThumb;

	private bool _lDown;

	private bool _rDown;

	private bool _mDown;

	private string _lastWindow = "";

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int vKey);

	public RecorderForm()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Expected O, but got Unknown
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Expected O, but got Unknown
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Expected O, but got Unknown
		//IL_0283: Unknown result type (might be due to invalid IL or missing references)
		//IL_028d: Expected O, but got Unknown
		((Control)this).Text = "Teach Claude";
		((Form)this).FormBorderStyle = (FormBorderStyle)5;
		((Form)this).TopMost = true;
		((Form)this).StartPosition = (FormStartPosition)0;
		((Form)this).Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - 340, Screen.PrimaryScreen.WorkingArea.Top + 20);
		((Form)this).ClientSize = new Size(320, 190);
		Label val = new Label
		{
			Text = "What are you about to show me? (optional)",
			Left = 10,
			Top = 8,
			Width = 300
		};
		((Control)_note).SetBounds(10, 28, 300, 52);
		((TextBoxBase)_note).Multiline = true;
		Label val2 = new Label
		{
			Text = "Screenshot every (sec):",
			Left = 10,
			Top = 90,
			Width = 150
		};
		((Control)_interval).SetBounds(165, 87, 55, 24);
		_interval.Minimum = 1m;
		_interval.Maximum = 10m;
		_interval.Value = 1m;
		((Control)_btn).SetBounds(10, 118, 300, 36);
		((Control)_btn).Text = "● Start recording";
		((Control)_btn).Font = new Font(((Control)this).Font.FontFamily, 10f, (FontStyle)1);
		((Control)_btn).Click += delegate
		{
			if (_recording)
			{
				Stop();
			}
			else
			{
				Start();
			}
		};
		((Control)_status).SetBounds(10, 160, 300, 24);
		((Control)_status).Text = "Ready. Nothing is recorded until you press Start.";
		((Control)this).Controls.AddRange((Control[])(object)new Control[6]
		{
			(Control)val,
			(Control)_note,
			(Control)val2,
			(Control)_interval,
			(Control)_btn,
			(Control)_status
		});
		_frameTimer.Tick += delegate
		{
			CaptureFrame();
		};
		_clickTimer.Tick += delegate
		{
			PollClicks();
		};
		((Form)this).FormClosing += (FormClosingEventHandler)delegate
		{
			if (_recording)
			{
				Stop();
			}
		};
	}

	private void Start()
	{
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "TeachClaude");
		_dir = Path.Combine(path, "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
		Directory.CreateDirectory(_dir);
		File.WriteAllText(Path.Combine(_dir, "note.txt"), string.IsNullOrWhiteSpace(((Control)_note).Text) ? "(no note)" : ((Control)_note).Text.Trim(), Encoding.UTF8);
		_log = new StreamWriter(Path.Combine(_dir, "log.txt"), append: false, Encoding.UTF8)
		{
			AutoFlush = true
		};
		_log.WriteLine("Teach Claude session started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
		_log.WriteLine("Screen: " + Screen.PrimaryScreen.Bounds.Width + "x" + Screen.PrimaryScreen.Bounds.Height);
		_log.WriteLine("Format: [seconds] event");
		_frames = 0;
		_skipped = 0;
		_lastThumb = null;
		_lastWindow = "";
		_clock = Stopwatch.StartNew();
		_recording = true;
		_frameTimer.Interval = (int)_interval.Value * 1000;
		_frameTimer.Start();
		_clickTimer.Start();
		((Control)_btn).Text = "■ Stop and save";
		((Control)_btn).BackColor = Color.IndianRed;
		((Control)_interval).Enabled = false;
		((Control)_note).Enabled = false;
		CaptureFrame();
	}

	private void Stop()
	{
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		_recording = false;
		_frameTimer.Stop();
		_clickTimer.Stop();
		_log.WriteLine("[" + Secs() + "] STOP  frames=" + _frames + " skipped_unchanged=" + _skipped);
		_log.Dispose();
		string text = _dir + ".zip";
		try
		{
			if (File.Exists(text))
			{
				File.Delete(text);
			}
			ZipFile.CreateFromDirectory(_dir, text);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Saved the frames but could not zip them:\n" + ex.Message + "\n\nFolder: " + _dir);
		}
		((Control)_btn).Text = "● Start recording";
		((Control)_btn).BackColor = SystemColors.Control;
		((Control)_interval).Enabled = true;
		((Control)_note).Enabled = true;
		((Control)_status).Text = "Saved " + _frames + " frames.";
		if (File.Exists(text))
		{
			MessageBox.Show("Saved " + _frames + " screenshots.\n\nNow upload this zip file to Claude in the chat:\n" + text, "Teach Claude");
			try
			{
				Process.Start("explorer.exe", "/select,\"" + text + "\"");
			}
			catch
			{
			}
		}
	}

	private string Secs()
	{
		return ((double)_clock.ElapsedMilliseconds / 1000.0).ToString("0.0");
	}

	private static string ActiveWindowTitle()
	{
		StringBuilder stringBuilder = new StringBuilder(256);
		GetWindowText(GetForegroundWindow(), stringBuilder, stringBuilder.Capacity);
		return stringBuilder.ToString();
	}

	private void CaptureFrame()
	{
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Expected O, but got Unknown
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Expected O, but got Unknown
		if (!_recording)
		{
			return;
		}
		if (_clock.Elapsed.TotalMinutes >= 30.0 || _frames >= 2000)
		{
			Stop();
			return;
		}
		Rectangle bounds = Screen.PrimaryScreen.Bounds;
		Bitmap val = new Bitmap(bounds.Width, bounds.Height, (PixelFormat)137224);
		try
		{
			Graphics val2 = Graphics.FromImage((Image)(object)val);
			try
			{
				val2.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
				Point position = Cursor.Position;
				Pen val3 = new Pen(Color.Red, 3f);
				try
				{
					val2.DrawEllipse(val3, position.X - bounds.X - 14, position.Y - bounds.Y - 14, 28, 28);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			double[] array = Thumbnail(val);
			if (_lastThumb != null && MeanDiff(array, _lastThumb) < 2.0)
			{
				_skipped++;
				UpdateStatus();
				return;
			}
			_lastThumb = array;
			_frames++;
			int num = Math.Min(1600, bounds.Width);
			int height = (int)Math.Round((double)bounds.Height * ((double)num / (double)bounds.Width));
			string text = "frame_" + _frames.ToString("0000") + "_t" + Secs().Replace('.', '_') + "s.jpg";
			Bitmap val4 = new Bitmap((Image)(object)val, new Size(num, height));
			try
			{
				SaveJpeg(val4, Path.Combine(_dir, text));
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
			string text2 = ActiveWindowTitle();
			_log.WriteLine("[" + Secs() + "] FRAME " + text + "  window='" + text2 + "'");
			UpdateStatus();
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void PollClicks()
	{
		if (_recording)
		{
			Track(1, ref _lDown, "LEFT-CLICK");
			Track(2, ref _rDown, "RIGHT-CLICK");
			Track(4, ref _mDown, "MIDDLE-CLICK");
			string text = ActiveWindowTitle();
			if (text != _lastWindow)
			{
				_lastWindow = text;
				_log.WriteLine("[" + Secs() + "] FOCUS window='" + text + "'");
			}
		}
	}

	private void Track(int vk, ref bool wasDown, string name)
	{
		bool flag = (GetAsyncKeyState(vk) & 0x8000) != 0;
		if (flag && !wasDown)
		{
			Point position = Cursor.Position;
			_log.WriteLine("[" + Secs() + "] " + name + " at (" + position.X + "," + position.Y + ")  window='" + ActiveWindowTitle() + "'");
		}
		wasDown = flag;
	}

	private void UpdateStatus()
	{
		((Control)_status).Text = "● Recording  " + Secs() + " s   frames: " + _frames;
	}

	private static double[] Thumbnail(Bitmap src)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		double[] array = new double[576];
		Bitmap val = new Bitmap((Image)(object)src, new Size(32, 18));
		try
		{
			for (int i = 0; i < 18; i++)
			{
				for (int j = 0; j < 32; j++)
				{
					Color pixel = val.GetPixel(j, i);
					array[i * 32 + j] = 0.299 * (double)(int)pixel.R + 0.587 * (double)(int)pixel.G + 0.114 * (double)(int)pixel.B;
				}
			}
			return array;
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private static double MeanDiff(double[] a, double[] b)
	{
		double num = 0.0;
		for (int i = 0; i < a.Length; i++)
		{
			num += Math.Abs(a[i] - b[i]);
		}
		return num / (double)a.Length;
	}

	private static void SaveJpeg(Bitmap bmp, string path)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Expected O, but got Unknown
		ImageCodecInfo val = ImageCodecInfo.GetImageEncoders().First((ImageCodecInfo c) => c.FormatID == ImageFormat.Jpeg.Guid);
		EncoderParameters val2 = new EncoderParameters(1);
		try
		{
			val2.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 70L);
			((Image)bmp).Save(path, val, val2);
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}
}
