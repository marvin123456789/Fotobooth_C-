// File: FotoboxForm.cs
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;

namespace FotoboxApp
{
    public partial class FotoboxForm : Form
    {
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private Bitmap latestFrame;
        private System.Windows.Forms.Timer countdownTimer;
        private int countdown = 3;
        private int printerIndex = 0;
        private readonly string[] printers = { "Drucker1", "Drucker2" };

        private PictureBox pictureBoxLive;
        private OverlayForm overlayForm;
        private PictureBox pictureBoxPreview;
        private Button btnDrucken;

        public FotoboxForm()
        {
            InitializeLayout();
            InitializeCountdown();
        }

        private void InitializeCamera()
        {
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices.Count == 0)
            {
                MessageBox.Show("Keine Webcam gefunden.");
                return;
            }
            videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
            videoSource.NewFrame += VideoSource_NewFrame;
            videoSource.Start();
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                Debug.WriteLine("📸 Frame received");
                Bitmap frame = (Bitmap)eventArgs.Frame.Clone();

                pictureBoxLive.Invoke(new MethodInvoker(() =>
                {
                    pictureBoxLive.Image?.Dispose();
                    pictureBoxLive.Image = (Bitmap)frame.Clone();
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Fehler in NewFrame: {ex.Message}");
            }
        }

        private void InitializeCountdown()
        {
            countdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            countdownTimer.Tick += (s, e) =>
            {
                countdown--;
                overlayForm.UpdateCountdown(countdown);
                if (countdown == 0)
                {
                    countdownTimer.Stop();
                    overlayForm.HideCountdown();
                    TakePhoto();
                    countdown = 3;
                }
            };
        }

        private void btnFotoMachen_Click(object sender, EventArgs e)
        {
            InitializeCamera();
            overlayForm.UpdateCountdown(countdown);
            overlayForm.ShowCountdown();
            countdownTimer.Start();
        }

        private void TakePhoto()
        {
            if (latestFrame == null) return;

            string path = Path.Combine(Environment.CurrentDirectory, "foto.jpg");
            using (Bitmap final = AddFrame((Bitmap)latestFrame.Clone()))
            {
                final.Save(path);
            }
            pictureBoxPreview.Image = Image.FromFile(path);
        }

        private Bitmap AddFrame(Bitmap original)
        {
            Bitmap frame = new Bitmap(original.Width, original.Height);
            using (Graphics g = Graphics.FromImage(frame))
            {
                g.DrawImage(original, 0, 0);
                g.DrawRectangle(Pens.Red, 10, 10, original.Width - 20, original.Height - 20);
            }
            return frame;
        }

        private void btnDrucken_Click(object sender, EventArgs e)
        {
            PrintDocument pd = new PrintDocument();
            pd.PrinterSettings.PrinterName = printers[printerIndex % 2];
            pd.PrintPage += (s, ev) =>
            {
                Image img = pictureBoxPreview.Image;
                ev.Graphics.DrawImage(img, ev.MarginBounds);
            };
            pd.Print();
            printerIndex++;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (videoSource != null && videoSource.IsRunning)
                videoSource.SignalToStop();
        }

        private void InitializeLayout()
        {
            this.Text = "Fotobox";
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;

            pictureBoxLive = new PictureBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            this.Controls.Add(pictureBoxLive);
            pictureBoxLive.SendToBack();

            overlayForm = new OverlayForm();
            overlayForm.StartPosition = FormStartPosition.Manual;
            overlayForm.Location = this.Location;
            overlayForm.Size = this.Size;
            overlayForm.TopMost = true;
            overlayForm.Show();

            overlayForm.FotoMachenClicked += btnFotoMachen_Click;

            this.Resize += (s, e) => overlayForm.Size = this.Size;
            this.Move += (s, e) => overlayForm.Location = this.Location;
        }
    }

    public class OverlayForm : Form
    {
        private Button btnFotoMachen;
        private Label lblCountdown;

        public OverlayForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black;
            this.Opacity = 0.3;
            this.TransparencyKey = Color.Empty;

            btnFotoMachen = new Button
            {
                Text = "📸 Foto aufnehmen",
                Font = new Font("Arial", 24, FontStyle.Bold),
                BackColor = Color.Red,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Standard,
                Visible = true,
                Size = new Size(400, 90)
            };
            btnFotoMachen.Click += (s, e) => OnFotoMachenClicked();
            this.Controls.Add(btnFotoMachen);

            lblCountdown = new Label
            {
                Font = new Font("Arial", 72, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Black,
                AutoSize = true,
                Visible = false
            };
            this.Controls.Add(lblCountdown);

            this.Load += (s, e) => CenterOverlayElements();
            this.Resize += (s, e) => CenterOverlayElements();
        }

        public event EventHandler FotoMachenClicked;

        private void OnFotoMachenClicked()
        {
            FotoMachenClicked?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateCountdown(int value)
        {
            lblCountdown.Text = value.ToString();
        }

        public void ShowCountdown()
        {
            lblCountdown.Visible = true;
        }

        public void HideCountdown()
        {
            lblCountdown.Visible = false;
        }

        private void CenterOverlayElements()
        {
            btnFotoMachen.Location = new Point(
                (this.ClientSize.Width - btnFotoMachen.Width) / 2,
                this.ClientSize.Height - btnFotoMachen.Height - 50
            );

            lblCountdown.Location = new Point(
                (this.ClientSize.Width - lblCountdown.Width) / 2,
                100
            );
        }
    }
} // end namespace