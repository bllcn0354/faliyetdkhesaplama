using System;
using System.Collections.Generic;
using System.ComponentModel;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.IO; 

namespace faliyetdkhesaplama
{
    public partial class hesapla : Form
    {

        string _imgPath;
        bool _drag;
        Point _start;
        Rectangle _roi = Rectangle.Empty;


        public hesapla()
        {
            InitializeComponent();
            this.KeyPreview = true;                  // Ctrl+V'yi form yakalasın
            pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
            pictureBox1.MouseDown += (s, e) => { _drag = true; _start = e.Location; };
            pictureBox1.MouseMove += (s, e) =>
            {
                if (_drag && pictureBox1.Image != null)
                {
                    var x = Math.Min(_start.X, e.X);
                    var y = Math.Min(_start.Y, e.Y);
                    var w = Math.Abs(e.X - _start.X);
                    var h = Math.Abs(e.Y - _start.Y);
                    _roi = new Rectangle(x, y, w, h);
                    pictureBox1.Invalidate();
                }
            };
            pictureBox1.MouseUp += (s, e) => { _drag = false; };
            pictureBox1.Paint += (s, e) =>
            {
                if (_roi.Width > 0 && _roi.Height > 0)
                    e.Graphics.DrawRectangle(Pens.Red, _roi);
            };

        }

        private string SaveImageToTemp(Image img)
        {
            var path = Path.Combine(Path.GetTempPath(), $"paste_{Guid.NewGuid():N}.png");
            using var bmp = new Bitmap(img);
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return path;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.V))
            {
                PasteImageFromClipboard();
                return true; // kısayolu tükettik
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void PasteImageFromClipboard()
        {
            if (Clipboard.ContainsImage())
            {
                var img = Clipboard.GetImage();
                if (pictureBox1.Image != null) pictureBox1.Image.Dispose();
                pictureBox1.Image = new Bitmap(img);
                _imgPath = SaveImageToTemp(img);       // *** KRİTİK ***
                _roi = Rectangle.Empty;
            }
            else if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                foreach (string f in files)
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
                    {
                        using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var tmp = new Bitmap(fs);
                        if (pictureBox1.Image != null) pictureBox1.Image.Dispose();
                        pictureBox1.Image = new Bitmap(tmp);
                        _imgPath = f;                   // *** KRİTİK ***
                        _roi = Rectangle.Empty;
                        break;
                    }
                }
            }
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _imgPath = ofd.FileName;
                pictureBox1.Image = Image.FromFile(_imgPath);
                _roi = Rectangle.Empty;
            }
        }

        private void btnOcr_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;

            if (string.IsNullOrEmpty(_imgPath))
                _imgPath = SaveImageToTemp(pictureBox1.Image);   // *** önemli ***

            Rectangle? roi = _roi.Width > 0 ? _roi : null;
            DataTable dt = OcrHelper.ExtractTimesFromImage(_imgPath, roi);

            dt.Columns.Add("Toplam Süre", typeof(TimeSpan));
            TimeSpan total = TimeSpan.Zero;
            foreach (DataRow dr in dt.Rows)
                if (TimeSpan.TryParse(dr["Harcanan"]?.ToString(), out var ts))
                    total += ts;

            dt.Rows.Add("Toplam", total); // <-- TimeSpan olarak ekle
            dataGridView1.DataSource = dt;

            // Görünüm formatı (opsiyonel)
            var col = dataGridView1.Columns["Toplam Süre"];
            if (col != null) col.DefaultCellStyle.Format = @"hh\:mm\:ss";
        }

        private void hesapla_Load(object sender, EventArgs e)
        {

        }
    }
}
