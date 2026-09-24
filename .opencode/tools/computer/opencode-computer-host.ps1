param()

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
[Console]::InputEncoding = New-Object System.Text.UTF8Encoding($false)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public class Win32 {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();

  public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder text, int count);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr hWnd, StringBuilder text, int count);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);
  [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hWnd, ref POINT pt);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);
  [DllImport("user32.dll")] public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint uCode, uint uMapType);
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
  [DllImport("user32.dll")] public static extern IntPtr GetWindowDC(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
  [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT pt);
  [DllImport("user32.dll")] public static extern IntPtr ChildWindowFromPointEx(IntPtr hWndParent, POINT pt, uint flags);
  [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
  [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
  [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
  [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
  [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
  [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);

  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
  [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public INPUTUNION u; }
  [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION {
    [FieldOffset(0)] public MOUSEINPUT mi;
    [FieldOffset(0)] public KEYBDINPUT ki;
  }
  [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

  public static System.Drawing.Bitmap GrabScreen(int x, int y, int w, int h) {
    IntPtr src = GetDC(IntPtr.Zero);
    IntPtr mem = CreateCompatibleDC(src);
    IntPtr hbmp = CreateCompatibleBitmap(src, w, h);
    IntPtr old = SelectObject(mem, hbmp);
    BitBlt(mem, 0, 0, w, h, src, x, y, 0x00CC0020);
    SelectObject(mem, old);
    DeleteDC(mem);
    ReleaseDC(IntPtr.Zero, src);
    System.Drawing.Bitmap result = System.Drawing.Image.FromHbitmap(hbmp);
    DeleteObject(hbmp);
    return result;
  }

  public static System.Drawing.Bitmap GrabWindow(IntPtr hwnd, int w, int h) {
    IntPtr src = GetDC(IntPtr.Zero);
    IntPtr mem = CreateCompatibleDC(src);
    IntPtr hbmp = CreateCompatibleBitmap(src, w, h);
    IntPtr old = SelectObject(mem, hbmp);
    bool ok = PrintWindow(hwnd, mem, 2);
    SelectObject(mem, old);
    DeleteDC(mem);
    ReleaseDC(IntPtr.Zero, src);
    if (!ok) { DeleteObject(hbmp); return null; }
    System.Drawing.Bitmap result = System.Drawing.Image.FromHbitmap(hbmp);
    DeleteObject(hbmp);
    return result;
  }

  public static System.Drawing.Bitmap GrabRegion(System.Drawing.Bitmap source, int x, int y, int w, int h) {
    System.Drawing.Rectangle rect = new System.Drawing.Rectangle(x, y, w, h);
    return source.Clone(rect, source.PixelFormat);
  }

  public static void WriteJpeg(System.Drawing.Bitmap image, string file, long quality) {
    System.Drawing.Imaging.ImageCodecInfo codec = null;
    foreach (System.Drawing.Imaging.ImageCodecInfo candidate in System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()) {
      if (candidate.MimeType == "image/jpeg") { codec = candidate; break; }
    }
    System.Drawing.Imaging.EncoderParameters parameters = new System.Drawing.Imaging.EncoderParameters(1);
    parameters.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
    image.Save(file, codec, parameters);
  }

  public static int GetSystemDpi() {
    using (System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) { return (int)g.DpiX; }
  }

  static void SendVkRaw(int vk, bool isUp) {
    INPUT input = new INPUT();
    input.type = 1;
    input.u.ki = new KEYBDINPUT();
    input.u.ki.wVk = (ushort)vk;
    input.u.ki.wScan = (ushort)MapVirtualKey((uint)vk, 0);
    input.u.ki.dwFlags = isUp ? 0x0002u : 0u;
    INPUT[] arr = new INPUT[1];
    arr[0] = input;
    SendInput(1, arr, Marshal.SizeOf(typeof(INPUT)));
  }

  static void SendUnicodeRaw(char c) {
    INPUT[] arr = new INPUT[2];
    arr[0].type = 1; arr[0].u.ki.wScan = (ushort)c; arr[0].u.ki.dwFlags = 0x0004;
    arr[1].type = 1; arr[1].u.ki.wScan = (ushort)c; arr[1].u.ki.dwFlags = 0x0006;
    SendInput(2, arr, Marshal.SizeOf(typeof(INPUT)));
  }

  static bool BeginFocus(IntPtr hwnd, out uint foreThread, out uint thisThread) {
    IntPtr fg = GetForegroundWindow();
    uint fp = 0;
    foreThread = GetWindowThreadProcessId(fg, out fp);
    thisThread = GetCurrentThreadId();
    bool attached = thisThread != foreThread && AttachThreadInput(thisThread, foreThread, true);
    ShowWindow(hwnd, 9);
    BringWindowToTop(hwnd);
    SetForegroundWindow(hwnd);
    SwitchToThisWindow(hwnd, true);
    System.Threading.Thread.Sleep(120);
    return attached;
  }

  public static string FocusType(IntPtr hwnd, string text) {
    uint foreThread, thisThread;
    bool attached = BeginFocus(hwnd, out foreThread, out thisThread);
    try {
      int total = 0;
      foreach (char c in text) { SendUnicodeRaw(c); total += 2; System.Threading.Thread.Sleep(10); }
      return "聚焦并前台输入 " + total + " 事件";
    } finally {
      if (attached) AttachThreadInput(thisThread, foreThread, false);
    }
  }

  public static string FocusKey(IntPtr hwnd, int[] mods, int[] keys, string mode) {
    uint foreThread, thisThread;
    bool attached = BeginFocus(hwnd, out foreThread, out thisThread);
    try {
      if (mode == "up") {
        foreach (int k in keys) { SendVkRaw(k, true); System.Threading.Thread.Sleep(10); }
        for (int i = mods.Length - 1; i >= 0; i--) { SendVkRaw(mods[i], true); System.Threading.Thread.Sleep(10); }
      } else if (mode == "down") {
        foreach (int m in mods) { SendVkRaw(m, false); System.Threading.Thread.Sleep(10); }
        foreach (int k in keys) { SendVkRaw(k, false); System.Threading.Thread.Sleep(10); }
      } else {
        foreach (int m in mods) { SendVkRaw(m, false); System.Threading.Thread.Sleep(10); }
        foreach (int k in keys) { SendVkRaw(k, false); System.Threading.Thread.Sleep(20); SendVkRaw(k, true); }
        for (int i = mods.Length - 1; i >= 0; i--) { SendVkRaw(mods[i], true); System.Threading.Thread.Sleep(10); }
      }
      return "聚焦并前台按键完成";
    } finally {
      if (attached) AttachThreadInput(thisThread, foreThread, false);
    }
  }
}
'@

Add-Type -ReferencedAssemblies UIAutomationClient, UIAutomationTypes, WindowsBase -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Automation;
using System.Windows;
using System.Runtime.InteropServices;

public class Uia {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, string l);

  static string Esc(string s) {
    if (s == null) return "";
    StringBuilder b = new StringBuilder();
    foreach (char c in s) {
      if (c == '"' || c == '\\') b.Append('\\').Append(c);
      else if (c == '\n') b.Append("\\n");
      else if (c == '\r') b.Append("\\r");
      else if (c == '\t') b.Append("\\t");
      else if (c < ' ') b.Append(' ');
      else b.Append(c);
    }
    return b.ToString();
  }

  static ControlType TypeOf(string t) {
    if (string.IsNullOrEmpty(t)) return null;
    switch (t.ToLowerInvariant()) {
      case "button": return ControlType.Button;
      case "edit": return ControlType.Edit;
      case "document": return ControlType.Document;
      case "window": return ControlType.Window;
      case "menuitem": return ControlType.MenuItem;
      case "checkbox": return ControlType.CheckBox;
      case "radiobutton": return ControlType.RadioButton;
      case "combobox": return ControlType.ComboBox;
      case "listitem": return ControlType.ListItem;
      case "tabitem": return ControlType.TabItem;
      case "text": return ControlType.Text;
      case "pane": return ControlType.Pane;
      case "hyperlink": return ControlType.Hyperlink;
      case "treeitem": return ControlType.TreeItem;
      case "slider": return ControlType.Slider;
      case "spinner": return ControlType.Spinner;
      case "toolbar": return ControlType.ToolBar;
      case "menu": return ControlType.Menu;
      case "group": return ControlType.Group;
      case "image": return ControlType.Image;
      default: return null;
    }
  }

  static AutomationElement Find(IntPtr hwnd, string name, string id, string className, string type, int index) {
    AutomationElement root = AutomationElement.FromHandle(hwnd);
    if (root == null) return null;
    List<Condition> conds = new List<Condition>();
    if (!string.IsNullOrEmpty(name)) conds.Add(new PropertyCondition(AutomationElement.NameProperty, name));
    if (!string.IsNullOrEmpty(id)) conds.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, id));
    if (!string.IsNullOrEmpty(className)) conds.Add(new PropertyCondition(AutomationElement.ClassNameProperty, className));
    ControlType ct = TypeOf(type);
    if (ct != null) conds.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, ct));
    Condition cond;
    if (conds.Count == 0) cond = Condition.TrueCondition;
    else if (conds.Count == 1) cond = conds[0];
    else cond = new AndCondition(conds.ToArray());
    AutomationElementCollection found = root.FindAll(TreeScope.Descendants, cond);
    if (found.Count == 0) return null;
    int i = index < 0 ? 0 : index;
    if (i >= found.Count) i = found.Count - 1;
    return found[i];
  }

  public static string List(IntPtr hwnd, int max) {
    AutomationElement root = AutomationElement.FromHandle(hwnd);
    if (root == null) return "[]";
    AutomationElementCollection all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
    StringBuilder sb = new StringBuilder();
    sb.Append("[");
    int n = 0;
    foreach (AutomationElement e in all) {
      if (n >= max) break;
      AutomationElement.AutomationElementInformation c = e.Current;
      Rect r = c.BoundingRectangle;
      if (n > 0) sb.Append(",");
      sb.Append("{");
      sb.Append("\"type\":\"").Append(Esc(c.ControlType.ProgrammaticName.Replace("ControlType.", ""))).Append("\",");
      sb.Append("\"name\":\"").Append(Esc(c.Name)).Append("\",");
      sb.Append("\"id\":\"").Append(Esc(c.AutomationId)).Append("\",");
      sb.Append("\"class\":\"").Append(Esc(c.ClassName)).Append("\",");
      sb.Append("\"hwnd\":").Append(c.NativeWindowHandle).Append(",");
      sb.Append("\"enabled\":").Append(c.IsEnabled ? "true" : "false").Append(",");
      sb.Append("\"offscreen\":").Append(c.IsOffscreen ? "true" : "false").Append(",");
      sb.Append("\"x\":").Append((int)r.X).Append(",\"y\":").Append((int)r.Y).Append(",\"width\":").Append((int)r.Width).Append(",\"height\":").Append((int)r.Height);
      sb.Append("}");
      n++;
    }
    sb.Append("]");
    return sb.ToString();
  }

  public static string Act(IntPtr hwnd, string name, string id, string className, string type, int index, string action, string value) {
    AutomationElement e = Find(hwnd, name, id, className, type, index);
    if (e == null) return "not-found";
    object p;
    switch ((action ?? "").ToLowerInvariant()) {
      case "invoke":
      case "click":
        if (e.TryGetCurrentPattern(InvokePattern.Pattern, out p)) { ((InvokePattern)p).Invoke(); return "invoked"; }
        if (e.Current.NativeWindowHandle != 0) { SendMessage(new IntPtr(e.Current.NativeWindowHandle), 0x00F5, IntPtr.Zero, null); return "clicked-bm"; }
        return "no-invoke";
      case "setvalue":
        if (e.TryGetCurrentPattern(ValuePattern.Pattern, out p)) { ((ValuePattern)p).SetValue(value ?? ""); return "value-set"; }
        if (e.Current.NativeWindowHandle != 0) { SendMessage(new IntPtr(e.Current.NativeWindowHandle), 0x000C, IntPtr.Zero, value ?? ""); return "value-set-wm"; }
        return "no-value";
      case "getvalue":
        if (e.TryGetCurrentPattern(ValuePattern.Pattern, out p)) return ((ValuePattern)p).Current.Value;
        if (e.TryGetCurrentPattern(TextPattern.Pattern, out p)) return ((TextPattern)p).DocumentRange.GetText(-1);
        return "no-value";
      case "focus":
        e.SetFocus(); return "focused";
      case "toggle":
        if (e.TryGetCurrentPattern(TogglePattern.Pattern, out p)) { ((TogglePattern)p).Toggle(); return "toggled"; }
        return "no-toggle";
      case "select":
        if (e.TryGetCurrentPattern(SelectionItemPattern.Pattern, out p)) { ((SelectionItemPattern)p).Select(); return "selected"; }
        return "no-select";
      case "expand":
        if (e.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out p)) { ((ExpandCollapsePattern)p).Expand(); return "expanded"; }
        return "no-expand";
      case "collapse":
        if (e.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out p)) { ((ExpandCollapsePattern)p).Collapse(); return "collapsed"; }
        return "no-expand";
      case "info":
        {
          Rect r = e.Current.BoundingRectangle;
          return e.Current.ControlType.ProgrammaticName + "|" + e.Current.Name + "|id=" + e.Current.AutomationId + "|hwnd=" + e.Current.NativeWindowHandle + "|" + (int)r.X + "," + (int)r.Y + "," + (int)r.Width + "x" + (int)r.Height;
        }
      default: return "unknown-action";
    }
  }

  public static int FirstInputHwnd(IntPtr hwnd) {
    AutomationElement root = AutomationElement.FromHandle(hwnd);
    if (root == null) return 0;
    Condition cond = new OrCondition(
      new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document),
      new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)
    );
    AutomationElement e = root.FindFirst(TreeScope.Descendants, cond);
    if (e == null) return 0;
    return e.Current.NativeWindowHandle;
  }

  public static string TypeText(IntPtr hwnd, string text) {
    AutomationElement root = AutomationElement.FromHandle(hwnd);
    if (root == null) return null;
    Condition cond = new OrCondition(
      new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document),
      new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)
    );
    AutomationElement e = root.FindFirst(TreeScope.Descendants, cond);
    if (e == null) return null;
    object p;
    if (e.TryGetCurrentPattern(ValuePattern.Pattern, out p)) {
      try {
        ValuePattern vp = (ValuePattern)p;
        vp.SetValue((vp.Current.Value ?? "") + text);
        return "uia";
      } catch { return null; }
    }
    return null;
  }
}
'@

try { [void][Win32]::SetProcessDpiAwarenessContext([IntPtr](-4)) } catch { try { [void][Win32]::SetProcessDPIAware() } catch {} }

$script:SaveDir = Join-Path $env:TEMP "opencode-computer-use"
try {
  if (Test-Path $script:SaveDir) {
    Get-ChildItem -LiteralPath $script:SaveDir -File -ErrorAction SilentlyContinue |
      Where-Object { $_.LastWriteTime -lt (Get-Date).AddMinutes(-5) } |
      Remove-Item -Force -ErrorAction SilentlyContinue
  }
} catch {}

$script:VK = @{
  'backspace' = 0x08; 'tab' = 0x09; 'enter' = 0x0D; 'return' = 0x0D; 'shift' = 0x10; 'ctrl' = 0x11; 'control' = 0x11;
  'alt' = 0x12; 'menu' = 0x12; 'pause' = 0x13; 'capslock' = 0x14; 'esc' = 0x1B; 'escape' = 0x1B; 'space' = 0x20;
  'pageup' = 0x21; 'pgup' = 0x21; 'pagedown' = 0x22; 'pgdn' = 0x22; 'end' = 0x23; 'home' = 0x24;
  'left' = 0x25; 'up' = 0x26; 'right' = 0x27; 'down' = 0x28; 'printscreen' = 0x2C; 'insert' = 0x2D; 'delete' = 0x2E; 'del' = 0x2E;
  'win' = 0x5B; 'lwin' = 0x5B; 'rwin' = 0x5C; 'apps' = 0x5D;
  'num0' = 0x60; 'num1' = 0x61; 'num2' = 0x62; 'num3' = 0x63; 'num4' = 0x64; 'num5' = 0x65; 'num6' = 0x66; 'num7' = 0x67; 'num8' = 0x68; 'num9' = 0x69;
  'multiply' = 0x6A; 'add' = 0x6B; 'subtract' = 0x6D; 'decimal' = 0x6E; 'divide' = 0x6F;
  'f1' = 0x70; 'f2' = 0x71; 'f3' = 0x72; 'f4' = 0x73; 'f5' = 0x74; 'f6' = 0x75; 'f7' = 0x76; 'f8' = 0x77; 'f9' = 0x78; 'f10' = 0x79; 'f11' = 0x7A; 'f12' = 0x7B;
  'numlock' = 0x90; 'scrolllock' = 0x91;
  ';' = 0xBA; '=' = 0xBB; ',' = 0xBC; '-' = 0xBD; '.' = 0xBE; '/' = 0xBF; '`' = 0xC0; '[' = 0xDB; '\' = 0xDC; ']' = 0xDD; "'" = 0xDE
}
for ($i = 0; $i -lt 26; $i++) { $script:VK[[string][char](97 + $i)] = 0x41 + $i }
for ($i = 0; $i -lt 10; $i++) { $script:VK[[string]$i] = 0x30 + $i }

$script:SW = @{ restore = 9; minimize = 6; maximize = 3; show = 5; hide = 0 }
$script:VK_MODS = @{ 'ctrl' = 0x11; 'control' = 0x11; 'shift' = 0x10; 'alt' = 0x12; 'win' = 0x5B; 'lwin' = 0x5B; 'rwin' = 0x5C }

function Write-Response($id, $ok, $result, $errorMessage) {
  $obj = [ordered]@{ id = $id; ok = [bool]$ok }
  if ($ok) {
    if ($null -eq $result) { $result = @() }
    $obj.result = $result
  } elseif ($errorMessage) {
    $obj.error = [string]$errorMessage
  }
  $json = $obj | ConvertTo-Json -Depth 16 -Compress
  [Console]::Out.WriteLine($json)
  [Console]::Out.Flush()
}

function Get-WindowInfo($hwnd) {
  if (-not [Win32]::IsWindow($hwnd)) { return $null }
  $titleBuilder = New-Object System.Text.StringBuilder 512
  [void][Win32]::GetWindowTextW($hwnd, $titleBuilder, $titleBuilder.Capacity)
  $classBuilder = New-Object System.Text.StringBuilder 256
  [void][Win32]::GetClassNameW($hwnd, $classBuilder, $classBuilder.Capacity)
  $procId = 0
  [void][Win32]::GetWindowThreadProcessId($hwnd, [ref]$procId)
  $rect = New-Object Win32+RECT
  [void][Win32]::GetWindowRect($hwnd, [ref]$rect)
  $client = New-Object Win32+RECT
  [void][Win32]::GetClientRect($hwnd, [ref]$client)
  $processName = ""
  try { $processName = (Get-Process -Id $procId -ErrorAction SilentlyContinue).ProcessName } catch {}
  return [ordered]@{
    hwnd = $hwnd.ToInt64()
    title = $titleBuilder.ToString()
    className = $classBuilder.ToString()
    pid = $procId
    process = $processName
    x = $rect.Left
    y = $rect.Top
    width = ($rect.Right - $rect.Left)
    height = ($rect.Bottom - $rect.Top)
    clientWidth = $client.Right
    clientHeight = $client.Bottom
    visible = [Win32]::IsWindowVisible($hwnd)
    minimized = [Win32]::IsIconic($hwnd)
    foreground = ($hwnd -eq [Win32]::GetForegroundWindow())
  }
}

function Get-Windows($includeHidden) {
  $script:enumList = New-Object System.Collections.ArrayList
  $callback = [Win32+EnumWindowsProc]{
    param([IntPtr]$hWnd, [IntPtr]$lParam)
    if ($includeHidden -or [Win32]::IsWindowVisible($hWnd)) {
      $info = Get-WindowInfo $hWnd
      if ($null -ne $info) { [void]$script:enumList.Add($info) }
    }
    return $true
  }
  [void][Win32]::EnumWindows($callback, [IntPtr]::Zero)
  return @($script:enumList)
}

function Get-Target($p) {
  if ($null -ne $p -and $null -ne $p.hwnd -and [int64]$p.hwnd -ne 0) {
    $info = Get-WindowInfo ([IntPtr][int64]$p.hwnd)
    if ($null -eq $info) { throw "找不到 hwnd=$($p.hwnd) 的窗口" }
    return $info
  }
  $candidates = @(Get-Windows $false)
  if ($p.process) { $candidates = @($candidates | Where-Object { $_.process -and ($_.process -ieq [string]$p.process -or $_.process -ilike "*$([string]$p.process)*") }) }
  if ($p.title) { $candidates = @($candidates | Where-Object { $_.title -and ($_.title -ilike "*$([string]$p.title)*") }) }
  if ($p.class) { $candidates = @($candidates | Where-Object { $_.className -and ($_.className -ilike "*$([string]$p.class)*") }) }
  if ($p.pid) { $candidates = @($candidates | Where-Object { $_.pid -eq [int]$p.pid }) }
  $candidates = @($candidates | Where-Object { $_.title -ne "" })
  if ($candidates.Count -eq 0) { throw "找不到匹配窗口 (process=$($p.process), title=$($p.title), class=$($p.class), pid=$($p.pid))" }
  $best = $candidates[0]
  foreach ($candidate in $candidates) {
    if ($candidate.foreground -and -not $best.foreground) { $best = $candidate; continue }
    if ($best.foreground -and -not $candidate.foreground) { continue }
    if ($best.minimized -and -not $candidate.minimized) { $best = $candidate; continue }
  }
  return $best
}

function Focus-Window($hwnd) {
  if ([Win32]::IsIconic($hwnd)) { [void][Win32]::ShowWindow($hwnd, $script:SW.restore) }
  $foreground = [Win32]::GetForegroundWindow()
  $forePid = 0
  $foreThread = [Win32]::GetWindowThreadProcessId($foreground, [ref]$forePid)
  $thisThread = [Win32]::GetCurrentThreadId()
  $attached = $false
  if ($thisThread -ne $foreThread) { $attached = [Win32]::AttachThreadInput($thisThread, $foreThread, $true) }
  try {
    [void][Win32]::ShowWindow($hwnd, $script:SW.restore)
    [void][Win32]::BringWindowToTop($hwnd)
    [void][Win32]::SetForegroundWindow($hwnd)
    [Win32]::SwitchToThisWindow($hwnd, $true)
  } finally {
    if ($attached) { [void][Win32]::AttachThreadInput($thisThread, $foreThread, $false) }
  }
  Start-Sleep -Milliseconds 80
}

function Resolve-Key($name) {
  $lower = ([string]$name).ToLower().Trim()
  if ($script:VK.ContainsKey($lower)) { return [int]$script:VK[$lower] }
  if ($lower.Length -eq 1) { return [int][char]$lower.ToUpper()[0] }
  throw "未知按键: $name"
}

function Send-VK($vk, $isUp) {
  $input = New-Object Win32+INPUT
  $input.type = 1
  $input.u.ki = New-Object Win32+KEYBDINPUT
  $input.u.ki.wVk = [uint16]$vk
  $input.u.ki.wScan = [uint16][Win32]::MapVirtualKey([uint32]$vk, 0)
  $flags = 0
  if ($isUp) { $flags = 0x0002 }
  $input.u.ki.dwFlags = [uint32]$flags
  $input.u.ki.time = 0
  $input.u.ki.dwExtraInfo = [IntPtr]::Zero
  $arr = New-Object 'Win32+INPUT[]' 1
  $arr[0] = $input
  $sent = [Win32]::SendInput(1, $arr, [System.Runtime.InteropServices.Marshal]::SizeOf([type][Win32+INPUT]))
  if ($sent -eq 0) { throw "SendInput 失败（0 事件写入；可能被 UIPI 或会话隔离拦截）" }
}

function Send-UnicodeChar($codeUnit) {
  foreach ($isUp in @($false, $true)) {
    $input = New-Object Win32+INPUT
    $input.type = 1
    $input.u.ki = New-Object Win32+KEYBDINPUT
    $input.u.ki.wVk = 0
    $input.u.ki.wScan = [uint16]$codeUnit
    $flags = 0x0004
    if ($isUp) { $flags = $flags -bor 0x0002 }
    $input.u.ki.dwFlags = [uint32]$flags
    $input.u.ki.time = 0
    $input.u.ki.dwExtraInfo = [IntPtr]::Zero
    $arr = New-Object 'Win32+INPUT[]' 1
    $arr[0] = $input
    $sent = [Win32]::SendInput(1, $arr, [System.Runtime.InteropServices.Marshal]::SizeOf([type][Win32+INPUT]))
    if ($sent -eq 0) { throw "SendInput 失败（0 事件写入；可能被 UIPI 或会话隔离拦截）" }
  }
}

function Convert-Point($hwnd, $x, $y, $space, $toScreen) {
  $pt = New-Object Win32+POINT
  $pt.X = [int]$x
  $pt.Y = [int]$y
  $useClient = ($space -eq "client")
  if ($useClient) {
    if ($toScreen) { [void][Win32]::ClientToScreen($hwnd, [ref]$pt) }
  } else {
    if (-not $toScreen) { [void][Win32]::ScreenToClient($hwnd, [ref]$pt) }
  }
  return $pt
}

function Get-MouseFlags($button) {
  switch (([string]$button).ToLower()) {
    "right" { return @{ down = 0x0008; up = 0x0010; mk = 0x0002 } }
    "middle" { return @{ down = 0x0020; up = 0x0040; mk = 0x0010 } }
    default { return @{ down = 0x0002; up = 0x0004; mk = 0x0001 } }
  }
}

function Invoke-Screenshot($p) {
  $mode = if ($p.mode) { [string]$p.mode } else { "auto" }
  if ($p.out_path) {
    $file = [string]$p.out_path
    $parent = Split-Path -Parent $file
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
  } else {
    $saveDir = Join-Path $env:TEMP "opencode-computer-use"
    New-Item -ItemType Directory -Force -Path $saveDir | Out-Null
    $file = Join-Path $saveDir ("shot-" + [Guid]::NewGuid().ToString("N") + ".jpg")
  }
  $target = $null
  $bitmap = $null
  $captureMode = "screen"
  $didRestore = $false
  $restoreHwnd = [IntPtr]::Zero
  try {
    if ($p.display -ne $null -and -not $p.hwnd -and -not $p.process -and -not $p.title) {
      $screens = [System.Windows.Forms.Screen]::AllScreens
      $bounds = $screens[[int]$p.display].Bounds
      $bitmap = [Win32]::GrabScreen($bounds.X, $bounds.Y, $bounds.Width, $bounds.Height)
      $captureMode = "display"
    } else {
      $target = Get-Target $p
      $hwnd = [IntPtr][int64]$target.hwnd
      if ($target.minimized -and $mode -ne "screen") {
        [void][Win32]::ShowWindow($hwnd, 4)
        Start-Sleep -Milliseconds 200
        $didRestore = $true
        $restoreHwnd = $hwnd
        $target = Get-WindowInfo $hwnd
      }
      $windowRect = New-Object Win32+RECT
      [void][Win32]::GetWindowRect($hwnd, [ref]$windowRect)
      $w = $windowRect.Right - $windowRect.Left
      $h = $windowRect.Bottom - $windowRect.Top
      if ($w -le 0 -or $h -le 0) { throw "窗口尺寸无效" }

      $clientPt = New-Object Win32+POINT
      $clientPt.X = 0; $clientPt.Y = 0
      [void][Win32]::ClientToScreen($hwnd, [ref]$clientPt)
      $clientOffsetX = $clientPt.X - $windowRect.Left
      $clientOffsetY = $clientPt.Y - $windowRect.Top

      if ($mode -ne "screen") {
        $bitmap = [Win32]::GrabWindow($hwnd, $w, $h)
        if ($null -ne $bitmap) { $captureMode = "printwindow" }
      }

      if ($null -eq $bitmap) {
        Focus-Window $hwnd
        Start-Sleep -Milliseconds 120
        $windowRect = New-Object Win32+RECT
        [void][Win32]::GetWindowRect($hwnd, [ref]$windowRect)
        $w = $windowRect.Right - $windowRect.Left
        $h = $windowRect.Bottom - $windowRect.Top
        $bitmap = [Win32]::GrabScreen($windowRect.Left, $windowRect.Top, $w, $h)
        $captureMode = "screen"
      }

      $space = if ($p.space) { [string]$p.space } elseif ($p.client) { "client" } else { "window" }
      $baseX = 0; $baseY = 0
      if ($space -eq "client") { $baseX = $clientOffsetX; $baseY = $clientOffsetY }

      if ($p.region) {
        $cropped = [Win32]::GrabRegion($bitmap, [int]$p.region.x + $baseX, [int]$p.region.y + $baseY, [int]$p.region.width, [int]$p.region.height)
        $bitmap.Dispose()
        $bitmap = $cropped
      } elseif ($space -eq "client") {
        $cropped = [Win32]::GrabRegion($bitmap, $clientOffsetX, $clientOffsetY, $target.clientWidth, $target.clientHeight)
        $bitmap.Dispose()
        $bitmap = $cropped
      }
    }

    [Win32]::WriteJpeg($bitmap, $file, [int64]80)
    $width = $bitmap.Width
    $height = $bitmap.Height
    $dpiX = [Win32]::GetSystemDpi()
    $info = [ordered]@{
      path = $file
      mime = "image/jpeg"
      width = $width
      height = $height
      mode = $captureMode
      dpiX = $dpiX
      scale = [math]::Round($dpiX / 96, 2)
    }
    if ($null -ne $target) { $info.window = $target }
    return $info
  } finally {
    if ($null -ne $bitmap) { $bitmap.Dispose() }
    if ($didRestore -and -not $p.keep_restored -and $restoreHwnd -ne [IntPtr]::Zero) {
      try { [void][Win32]::ShowWindow($restoreHwnd, 6) } catch {}
    }
  }
}

function Invoke-Window($p) {
  $target = Get-Target $p
  $hwnd = [IntPtr][int64]$target.hwnd
  switch (([string]$p.action).ToLower()) {
    "focus" { Focus-Window $hwnd; return "已聚焦: $($target.title)" }
    "restore" { [void][Win32]::ShowWindow($hwnd, $script:SW.restore); return "已还原: $($target.title)" }
    "minimize" { [void][Win32]::ShowWindow($hwnd, $script:SW.minimize); return "已最小化: $($target.title)" }
    "maximize" { [void][Win32]::ShowWindow($hwnd, $script:SW.maximize); return "已最大化: $($target.title)" }
    "close" { [void][Win32]::PostMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero); return "已发送关闭: $($target.title)" }
    "move" { [void][Win32]::MoveWindow($hwnd, [int]$p.x, [int]$p.y, $target.width, $target.height, $true); return "已移动: $($target.title)" }
    "resize" { [void][Win32]::MoveWindow($hwnd, $target.x, $target.y, [int]$p.width, [int]$p.height, $true); return "已调整大小: $($target.title)" }
    default { throw "未知 window action: $($p.action)" }
  }
}

function Resolve-PointTarget($hwnd, $x, $y, $space) {
  $screen = $null
  if ($space -eq "client") {
    $screen = Convert-Point $hwnd $x $y "client" $true
  } else {
    $screen = New-Object Win32+POINT
    $screen.X = [int]$x; $screen.Y = [int]$y
  }
  $child = $hwnd
  $local = New-Object Win32+POINT
  $local.X = $screen.X; $local.Y = $screen.Y
  [void][Win32]::ScreenToClient($child, [ref]$local)
  $guard = 0
  while ($guard -lt 8) {
    $guard++
    $next = [Win32]::ChildWindowFromPointEx($child, $local, 3)
    if ($next -eq [IntPtr]::Zero -or $next -eq $child) { break }
    $child = $next
    $local = New-Object Win32+POINT
    $local.X = $screen.X; $local.Y = $screen.Y
    [void][Win32]::ScreenToClient($child, [ref]$local)
  }
  return @{ hwnd = $child; x = $local.X; y = $local.Y; screen = $screen }
}

function Invoke-Mouse($p) {
  $action = ([string]$p.action).ToLower()
  $flags = Get-MouseFlags $p.button
  $target = $null
  $hwnd = [IntPtr]::Zero
  if ($p.process -or $p.title -or $p.class -or $p.pid -or ($p.hwnd -and [int64]$p.hwnd -ne 0)) {
    $target = Get-Target $p
    $hwnd = [IntPtr][int64]$target.hwnd
  }
  $mode = if ($p.mode) { [string]$p.mode } elseif ($hwnd -ne [IntPtr]::Zero) { "postmessage" } else { "foreground" }
  $space = if ($p.space) { [string]$p.space } else { "client" }

  if ($mode -eq "postmessage") {
    if ($hwnd -eq [IntPtr]::Zero) { throw "postmessage 模式需要指定窗口 (process/title/hwnd)" }
    $resolved = Resolve-PointTarget $hwnd $p.x $p.y $space
    $postHwnd = $resolved.hwnd
    $lParam = ((($resolved.y -band 0xFFFF) -shl 16) -bor ($resolved.x -band 0xFFFF))
    switch ($action) {
      "move" { [void][Win32]::PostMessage($postHwnd, 0x0200, [IntPtr]::Zero, [IntPtr]$lParam) }
      "click" {
        $count = if ($p.count) { [int]$p.count } else { 1 }
        for ($i = 0; $i -lt $count; $i++) {
          [void][Win32]::PostMessage($postHwnd, 0x0200, [IntPtr]::Zero, [IntPtr]$lParam)
          [void][Win32]::PostMessage($postHwnd, [uint32](0x0200 + $flags.down - 0x0002 + 0x0001), [IntPtr]$flags.mk, [IntPtr]$lParam)
          Start-Sleep -Milliseconds 30
          [void][Win32]::PostMessage($postHwnd, [uint32](0x0200 + $flags.up - 0x0004 + 0x0002), [IntPtr]::Zero, [IntPtr]$lParam)
          Start-Sleep -Milliseconds 30
        }
      }
      "down" { [void][Win32]::PostMessage($postHwnd, [uint32](0x0200 + $flags.down - 0x0002 + 0x0001), [IntPtr]$flags.mk, [IntPtr]$lParam) }
      "up" { [void][Win32]::PostMessage($postHwnd, [uint32](0x0200 + $flags.up - 0x0004 + 0x0002), [IntPtr]::Zero, [IntPtr]$lParam) }
      "drag" {
        $from = Resolve-PointTarget $hwnd $p.x $p.y $space
        $toScreen = if ($space -eq "client") { Convert-Point $hwnd $p.to_x $p.to_y "client" $true } else { $pt = New-Object Win32+POINT; $pt.X = [int]$p.to_x; $pt.Y = [int]$p.to_y; $pt }
        $toLocal = New-Object Win32+POINT
        $toLocal.X = $toScreen.X; $toLocal.Y = $toScreen.Y
        [void][Win32]::ScreenToClient($from.hwnd, [ref]$toLocal)
        $fromLp = ((($from.y -band 0xFFFF) -shl 16) -bor ($from.x -band 0xFFFF))
        $toLp = ((($toLocal.Y -band 0xFFFF) -shl 16) -bor ($toLocal.X -band 0xFFFF))
        [void][Win32]::PostMessage($from.hwnd, 0x0200, [IntPtr]::Zero, [IntPtr]$fromLp)
        [void][Win32]::PostMessage($from.hwnd, 0x0201, [IntPtr]0x0001, [IntPtr]$fromLp)
        Start-Sleep -Milliseconds 50
        [void][Win32]::PostMessage($from.hwnd, 0x0200, [IntPtr]0x0001, [IntPtr]$toLp)
        Start-Sleep -Milliseconds 50
        [void][Win32]::PostMessage($from.hwnd, 0x0202, [IntPtr]::Zero, [IntPtr]$toLp)
      }
      "scroll" {
        $amount = if ($p.amount) { [int]$p.amount } else { 3 }
        $delta = 120 * $amount
        if (([string]$p.direction).ToLower() -eq "down") { $delta = -$delta }
        $wParam = ([int64]($delta -band 0xFFFF) -shl 16)
        $screenLp = ((($resolved.screen.Y -band 0xFFFF) -shl 16) -bor ($resolved.screen.X -band 0xFFFF))
        [void][Win32]::PostMessage($postHwnd, 0x020A, [IntPtr]$wParam, [IntPtr]$screenLp)
      }
      default { throw "未知 mouse action: $action" }
    }
    return "postmessage mouse $action 完成 (hwnd $($postHwnd.ToInt64()))"
  }

  $screenPt = $null
  if ($null -ne $p.x) {
    if ($hwnd -ne [IntPtr]::Zero -and $space -eq "client") {
      $screenPt = Convert-Point $hwnd $p.x $p.y "client" $true
    } else {
      $screenPt = New-Object Win32+POINT
      $screenPt.X = [int]$p.x; $screenPt.Y = [int]$p.y
    }
    [void][Win32]::SetCursorPos($screenPt.X, $screenPt.Y)
    Start-Sleep -Milliseconds 40
  }
  switch ($action) {
    "move" { }
    "click" {
      $count = if ($p.count) { [int]$p.count } else { 1 }
      for ($i = 0; $i -lt $count; $i++) {
        [Win32]::mouse_event([uint32]$flags.down, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 40
        [Win32]::mouse_event([uint32]$flags.up, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 40
      }
    }
    "down" { [Win32]::mouse_event([uint32]$flags.down, 0, 0, 0, [IntPtr]::Zero) }
    "up" { [Win32]::mouse_event([uint32]$flags.up, 0, 0, 0, [IntPtr]::Zero) }
    "drag" {
      $to = $null
      if ($hwnd -ne [IntPtr]::Zero -and $space -eq "client") {
        $to = Convert-Point $hwnd $p.to_x $p.to_y "client" $true
      } else {
        $to = New-Object Win32+POINT
        $to.X = [int]$p.to_x; $to.Y = [int]$p.to_y
      }
      [Win32]::mouse_event([uint32]$flags.down, 0, 0, 0, [IntPtr]::Zero)
      Start-Sleep -Milliseconds 60
      [void][Win32]::SetCursorPos($to.X, $to.Y)
      Start-Sleep -Milliseconds 60
      [Win32]::mouse_event([uint32]$flags.up, 0, 0, 0, [IntPtr]::Zero)
    }
    "scroll" {
      $amount = if ($p.amount) { [int]$p.amount } else { 3 }
      $delta = 120 * $amount
      if (([string]$p.direction).ToLower() -eq "down") { $delta = -$delta }
      $wheelData = [uint32]($delta -band 0xFFFFFFFF)
      [Win32]::mouse_event(0x0800, 0, 0, $wheelData, [IntPtr]::Zero)
    }
    default { throw "未知 mouse action: $action" }
  }
  return "foreground mouse $action 完成"
}

function Get-InputHwnd($hwnd) {
  if ($hwnd -eq [IntPtr]::Zero) { return $hwnd }
  try {
    $child = [Uia]::FirstInputHwnd($hwnd)
    if ($child -ne 0) { return [IntPtr]$child }
  } catch {}
  return $hwnd
}

function Test-InputResult($hwnd, $text) {
  if ($hwnd -eq [IntPtr]::Zero) { return "" }
  try {
    $v = [Uia]::Act($hwnd, $null, $null, $null, $null, -1, "getvalue", $null)
    if ($v -and $v -ne "no-value") {
      if ($v -like "*$text*") { return " [verified]" }
      return " [verify-mismatch: '$v']"
    }
  } catch {}
  return ""
}

function Invoke-Key($p) {
  $action = if ($p.action) { [string]$p.action } else { "press" }
  $target = $null
  $hwnd = [IntPtr]::Zero
  if ($p.process -or $p.title -or $p.class -or $p.pid -or ($p.hwnd -and [int64]$p.hwnd -ne 0)) {
    $target = Get-Target $p
    $hwnd = [IntPtr][int64]$target.hwnd
  }
  $mode = if ($p.mode) { [string]$p.mode } elseif ($hwnd -ne [IntPtr]::Zero) { "postmessage" } else { "foreground" }

  if ($p.text) {
    $text = [string]$p.text
    $verify = if ($p.verify -eq $false) { "" } else { "pending" }
    if ($mode -eq "postmessage") {
      if ($hwnd -eq [IntPtr]::Zero) { throw "postmessage 模式需要指定窗口" }
      if (-not $p.mode) {
        try { $via = [Uia]::TypeText($hwnd, $text) } catch { $via = $null }
        if ($via) {
          $suffix = if ($verify -eq "") { "" } else { Test-InputResult $hwnd $text }
          return "UIA 写入 $($text.Length) 字符$suffix"
        }
      }
      $input = Get-InputHwnd $hwnd
      foreach ($ch in $text.ToCharArray()) {
        [void][Win32]::PostMessage($input, 0x0102, [IntPtr][int]$ch, [IntPtr]1)
        Start-Sleep -Milliseconds 5
      }
      $suffix = if ($verify -eq "") { "" } else { Test-InputResult $hwnd $text }
      return "postmessage 输入 $($text.Length) 字符 (hwnd $($input.ToInt64()))$suffix"
    }
    if ($hwnd -ne [IntPtr]::Zero) {
      $r = [Win32]::FocusType($hwnd, $text)
      $suffix = if ($verify -eq "") { "" } else { Test-InputResult $hwnd $text }
      return "$r$suffix"
    }
    foreach ($ch in $text.ToCharArray()) { Send-UnicodeChar ([int]$ch) }
    return "foreground 输入 $($text.Length) 字符"
  }

  if (-not $p.combo) { throw "需要 combo 或 text" }
  $parts = @(([string]$p.combo).Split("+") | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" })
  if ($parts.Count -eq 0) { throw "combo 为空" }
  $mods = @()
  $keys = @()
  foreach ($part in $parts) {
    $lower = $part.ToLower()
    if ($script:VK_MODS.ContainsKey($lower)) { $mods += [int]$script:VK_MODS[$lower] } else { $keys += (Resolve-Key $part) }
  }

  if ($mode -eq "postmessage") {
    if ($hwnd -eq [IntPtr]::Zero) { throw "postmessage 模式需要指定窗口" }
    $input = Get-InputHwnd $hwnd
    foreach ($mod in $mods) { [void][Win32]::PostMessage($input, 0x0100, [IntPtr]$mod, [IntPtr]1) }
    foreach ($vk in $keys) {
      $scan = [Win32]::MapVirtualKey([uint32]$vk, 0)
      $downLp = 1 -bor ($scan -shl 16)
      [void][Win32]::PostMessage($input, 0x0100, [IntPtr]$vk, [IntPtr]$downLp)
      if ($action -eq "press") {
        $upLp = $downLp -bor 0xC0000000
        [void][Win32]::PostMessage($input, 0x0101, [IntPtr]$vk, [IntPtr]$upLp)
      }
    }
    if ($action -ne "down") { foreach ($mod in ($mods | Sort-Object -Descending)) { [void][Win32]::PostMessage($input, 0x0101, [IntPtr]$mod, [IntPtr](0xC0000001)) } }
    return "postmessage 按键 $($p.combo) 完成 (hwnd $($input.ToInt64()))"
  }

  if ($hwnd -ne [IntPtr]::Zero) {
    $result = [Win32]::FocusKey($hwnd, [int[]]$mods, [int[]]$keys, $action)
    if ($p.duration_ms) { Start-Sleep -Milliseconds ([int]$p.duration_ms) }
    return $result
  }
  foreach ($mod in $mods) { Send-VK $mod $false }
  foreach ($vk in $keys) {
    if ($action -eq "down") { Send-VK $vk $false }
    elseif ($action -eq "up") { Send-VK $vk $true }
    else { Send-VK $vk $false; Start-Sleep -Milliseconds 20; Send-VK $vk $true }
  }
  if ($action -ne "down") { foreach ($mod in ($mods | Sort-Object -Descending)) { Send-VK $mod $true } }
  if ($p.duration_ms) { Start-Sleep -Milliseconds ([int]$p.duration_ms) }
  return "foreground 按键 $($p.combo) 完成"
}

function Invoke-Clipboard($p) {
  $action = ([string]$p.action).ToLower()
  if ($action -eq "set") {
    Set-Clipboard -Value ([string]$p.text)
    return "剪贴板已写入 $(([string]$p.text).Length) 字符"
  }
  $text = Get-Clipboard -Raw -ErrorAction SilentlyContinue
  return [string]$text
}

function Invoke-PostMessage($p) {
  $target = Get-Target $p
  $hwnd = [IntPtr][int64]$target.hwnd
  $msg = 0
  if ($p.message -is [int] -or ($p.message -is [string] -and $p.message -match '^\d+$')) { $msg = [int]$p.message }
  else {
    $name = ([string]$p.message).ToUpper()
    $map = @{ "WM_NULL" = 0x0000; "WM_MOVE" = 0x0003; "WM_SIZE" = 0x0005; "WM_SETFOCUS" = 0x0007; "WM_KILLFOCUS" = 0x0008; "WM_CLOSE" = 0x0010; "WM_QUIT" = 0x0012; "WM_PAINT" = 0x000F; "WM_KEYDOWN" = 0x0100; "WM_KEYUP" = 0x0101; "WM_CHAR" = 0x0102; "WM_SYSKEYDOWN" = 0x0104; "WM_SYSKEYUP" = 0x0105; "WM_COMMAND" = 0x0111; "WM_LBUTTONDOWN" = 0x0201; "WM_LBUTTONUP" = 0x0202; "WM_RBUTTONDOWN" = 0x0204; "WM_RBUTTONUP" = 0x0205; "WM_MOUSEMOVE" = 0x0200; "WM_MOUSEWHEEL" = 0x020A; "WM_SETTEXT" = 0x000C }
    if (-not $map.ContainsKey($name)) { throw "未知 message: $($p.message)" }
    $msg = [int]$map[$name]
  }
  $wparam = if ($p.wparam) { [int64]$p.wparam } else { 0 }
  $lparam = if ($p.lparam) { [int64]$p.lparam } else { 0 }
  [void][Win32]::PostMessage($hwnd, [uint32]$msg, [IntPtr]$wparam, [IntPtr]$lparam)
  return "已发送 PostMessage 0x$("{0:X}" -f $msg) 到 $($target.title)"
}

function Invoke-WaitFor($p) {
  $condition = [string]$p.condition
  $timeout = if ($p.timeout_ms) { [int]$p.timeout_ms } else { 10000 }
  $interval = if ($p.interval_ms) { [int]$p.interval_ms } else { 250 }
  $start = Get-Date
  while ($true) {
    $found = $false
    $detail = $null
    try {
      switch ($condition) {
        "window" {
          $t = Get-Target $p
          if ($t) { $found = $true; $detail = $t.title }
        }
        "control" {
          $t = Get-Target $p
          $index = if ($p.index -ne $null) { [int]$p.index } else { -1 }
          $info = [Uia]::Act([IntPtr][int64]$t.hwnd, $p.name, $p.id, $p.class_name, $p.type, $index, "info", $null)
          if ($info -and $info -ne "not-found") { $found = $true; $detail = $info }
        }
        "file" {
          if ($p.path -and (Test-Path -LiteralPath ([string]$p.path))) { $found = $true; $detail = [string]$p.path }
        }
        default { throw "未知 wait_for condition: $condition" }
      }
    } catch {}
    if ($found) { return [ordered]@{ found = $true; elapsed_ms = [int]((Get-Date) - $start).TotalMilliseconds; detail = $detail } }
    if (((Get-Date) - $start).TotalMilliseconds -ge $timeout) {
      return [ordered]@{ found = $false; timeout = $true; elapsed_ms = [int]((Get-Date) - $start).TotalMilliseconds }
    }
    Start-Sleep -Milliseconds $interval
  }
}

function Invoke-Verify($p) {
  $check = [string]$p.check
  switch ($check) {
    "window" {
      try { $t = Get-Target $p; return [ordered]@{ ok = $true; title = $t.title; hwnd = $t.hwnd } }
      catch { return [ordered]@{ ok = $false; error = "窗口不存在" } }
    }
    "file" {
      $path = [string]$p.path
      if ($path -and (Test-Path -LiteralPath $path)) {
        $item = Get-Item -LiteralPath $path
        return [ordered]@{ ok = $true; path = $item.FullName; size = $item.Length; modified = $item.LastWriteTime.ToString("s") }
      }
      return [ordered]@{ ok = $false; error = "文件不存在"; path = $path }
    }
    "value" {
      $t = Get-Target $p
      $index = if ($p.index -ne $null) { [int]$p.index } else { -1 }
      $actual = [Uia]::Act([IntPtr][int64]$t.hwnd, $p.name, $p.id, $p.class_name, $p.type, $index, "getvalue", $null)
      $expected = [string]$p.expected
      $mode = if ($p.mode) { [string]$p.mode } else { "contains" }
      $ok = if ($actual -eq "no-value") { $false } elseif ($mode -eq "equals") { $actual -eq $expected } else { $actual -like "*$expected*" }
      return [ordered]@{ ok = $ok; actual = $actual; expected = $expected; mode = $mode }
    }
    "title" {
      $t = Get-Target $p
      $expected = [string]$p.expected
      $mode = if ($p.mode) { [string]$p.mode } else { "contains" }
      $ok = if ($mode -eq "equals") { $t.title -eq $expected } else { $t.title -like "*$expected*" }
      return [ordered]@{ ok = $ok; actual = $t.title; expected = $expected }
    }
    default { throw "未知 verify check: $check" }
  }
}

function Invoke-Action($action, $p) {
  if ($null -eq $p) { $p = @{} }
  switch (([string]$action).ToLower()) {
    "ping" { return @{ pong = $true } }
    "displays" {
      $screens = [System.Windows.Forms.Screen]::AllScreens
      $results = @()
      $dpiX = 96
      try { $g = [System.Drawing.Graphics]::FromHwnd([IntPtr]::Zero); $dpiX = $g.DpiX; $g.Dispose() } catch {}
      for ($i = 0; $i -lt $screens.Length; $i++) {
        $s = $screens[$i]
        $b = $s.Bounds
        $results += [ordered]@{ id = $i; name = $s.DeviceName; primary = [bool]$s.Primary; x = $b.X; y = $b.Y; width = $b.Width; height = $b.Height }
      }
      return [ordered]@{ dpiX = $dpiX; scale = [math]::Round($dpiX / 96, 2); screens = $results }
    }
    "windows" {
      $includeHidden = [bool]$p.include_hidden
      $list = @(Get-Windows $includeHidden)
      if ($p.query) {
        $q = [string]$p.query
        $list = @($list | Where-Object { $_.title -ilike "*$q*" -or $_.className -ilike "*$q*" -or $_.process -ilike "*$q*" })
      }
      return @($list | Where-Object { $_.title -ne "" -or $includeHidden })
    }
    "resolve" { return Get-Target $p }
    "screenshot" { return Invoke-Screenshot $p }
    "window" { return Invoke-Window $p }
    "mouse" { return Invoke-Mouse $p }
    "key" { return Invoke-Key $p }
    "clipboard" { return Invoke-Clipboard $p }
    "postmessage" { return Invoke-PostMessage $p }
    "controls" {
      $target = Get-Target $p
      $max = if ($p.max) { [int]$p.max } else { 200 }
      $json = [Uia]::List([IntPtr][int64]$target.hwnd, $max)
      return ($json | ConvertFrom-Json)
    }
    "control" {
      $target = Get-Target $p
      $index = if ($p.index -ne $null) { [int]$p.index } else { -1 }
      $hwnd = [IntPtr][int64]$target.hwnd
      $result = [Uia]::Act($hwnd, $p.name, $p.id, $p.class_name, $p.type, $index, $p.action, $p.value)
      if (([string]$p.action).ToLower() -eq "setvalue" -and $p.verify -ne $false -and $result -ne "not-found") {
        $actual = [Uia]::Act($hwnd, $p.name, $p.id, $p.class_name, $p.type, $index, "getvalue", $null)
        if ($actual -and $actual -ne "no-value") {
          $expected = [string]$p.value
          if ($actual -eq $expected -or $actual -like "*$expected*") { $result = "$result [verified]" }
          else { $result = "$result [verify-mismatch: '$actual']" }
        }
      }
      return $result
    }
    "wait_for" { return Invoke-WaitFor $p }
    "verify" { return Invoke-Verify $p }
    default { throw "未知 action: $action" }
  }
}

[Console]::Out.WriteLine('{"ok":true,"ready":true}')
[Console]::Out.Flush()

while ($true) {
  $line = [Console]::In.ReadLine()
  if ($null -eq $line) { break }
  if ($line.Trim() -eq "") { continue }
  $id = $null
  try {
    $command = $line | ConvertFrom-Json
    $id = $command.id
    $result = Invoke-Action $command.action $command.params
    Write-Response $id $true $result $null
  } catch {
    Write-Response $id $false $null $_.Exception.Message
  }
}
