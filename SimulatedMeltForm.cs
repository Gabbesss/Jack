using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopCloneMelt
{
    public class SimulatedMeltForm : Form
    {
        // Timer de WinForms (namespace explícito evita ambiguidade)
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly Random rnd = new Random();

        private Bitmap? desktopSnapshot;
        private readonly List<BounceIcon> clones = new List<BounceIcon>();
        private bool bouncePhase = false;
        private DateTime startTime;

        public SimulatedMeltForm()
        {
            // Janela em tela cheia, sempre no topo, com double-buffer para menos flicker
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            DoubleBuffered = true;

            // Permite sair com Esc ou F10
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.F10)
                    Close();
            };

            timer.Interval = 30; // ~33 fps
            timer.Tick += (s, e) => Tick();
            timer.Start();
            startTime = DateTime.Now;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Torna a janela clique-through (eventos passam para o desktop)
            int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
            SetWindowLong(Handle, GWL_EXSTYLE,
                exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
        }

        private void Tick()
        {
            CaptureDesktop();

            // Após 15s entra na fase de ricochete
            if (!bouncePhase && (DateTime.Now - startTime).TotalSeconds > 15)
            {
                bouncePhase = true;
                foreach (var c in clones)
                    c.StartBouncing();
            }

            // Adiciona novos ícones aleatoriamente
            if (rnd.NextDouble() < 0.1)
            {
                var img = PickIcon();
                clones.Add(new BounceIcon(
                    img,
                    rnd.Next(ClientSize.Width - 32),
                    rnd.Next(ClientSize.Height - 32),
                    rnd));
            }

            // Atualiza todos os ícones
            foreach (var c in clones)
                c.Update(ClientSize, bouncePhase);

            Invalidate(); // força repintura
        }

        private void CaptureDesktop()
        {
            var bounds = Screen.PrimaryScreen!.Bounds;
            desktopSnapshot?.Dispose();
            desktopSnapshot = new Bitmap(bounds.Width, bounds.Height);
            using var g = Graphics.FromImage(desktopSnapshot);
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }

        private Bitmap PickIcon()
        {
            return rnd.Next(4) switch
            {
                0 => SystemIcons.Error.ToBitmap(),
                1 => SystemIcons.Warning.ToBitmap(),
                2 => SystemIcons.Information.ToBitmap(),
                _ => SystemIcons.Question.ToBitmap()
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (desktopSnapshot == null) return;

            // Desenha a captura do desktop
            e.Graphics.DrawImage(desktopSnapshot, 0, 0);

            // Desenha todos os ícones clonados
            foreach (var c in clones)
                e.Graphics.DrawImage(c.Image, c.X, c.Y, 32, 32);

            // Ícone decorativo no cursor (não altera o ponteiro real)
            var mouse = PointToClient(MousePosition);
            e.Graphics.DrawImage(SystemIcons.Error.ToBitmap(),
                                 mouse.X - 16, mouse.Y - 16, 32, 32);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer.Stop();
            desktopSnapshot?.Dispose();
            foreach (var c in clones) c.Dispose();
            base.OnFormClosing(e);
        }

        // Classe de ícone com movimento
        private class BounceIcon : IDisposable
        {
            public Bitmap Image;
            public float X, Y, VX, VY;
            private readonly Random rnd;
            private bool bouncing;

            public BounceIcon(Bitmap img, int x, int y, Random rnd)
            {
                Image = img;
                X = x; Y = y;
                this.rnd = rnd;
                VX = rnd.Next(-1, 2);
                VY = rnd.Next(-1, 2);
            }

            public void StartBouncing()
            {
                bouncing = true;
                VX = rnd.Next(-6, 7);
                VY = rnd.Next(-6, 7);
                if (VX == 0) VX = 3;
                if (VY == 0) VY = 3;
            }

            public void Update(Size bounds, bool bouncePhase)
            {
                if (bouncing)
                {
                    X += VX;
                    Y += VY;
                    if (X < 0 || X > bounds.Width - 32) VX *= -1;
                    if (Y < 0 || Y > bounds.Height - 32) VY *= -1;
                }
                else
                {
                    // leve tremor antes do ricochete
                    X += rnd.Next(-1, 2);
                    Y += rnd.Next(-1, 2);
                }
            }

            public void Dispose() => Image.Dispose();
        }

        // WinAPI para clique-through
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
