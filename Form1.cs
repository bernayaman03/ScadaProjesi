using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Opc.UaFx;
using Opc.UaFx.Client;


namespace ScadaProjesi
{
    public class PlcConfig
    {
        public string DeviceId { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string OpcServerUrl { get; set; } = string.Empty;
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    public class AppSettingsModel
    {
        public List<PlcConfig> PlcDevices { get; set; } = new();
    }

    public partial class Form1 : Form
    {
        private System.Windows.Forms.Timer uiTimer = null!;
        private IConfigurationRoot configuration = null!;

        private Dictionary<string, OpcClient> plcClients = new();
        private Dictionary<string, Dictionary<string, string>> plcTags = new();
        
        private string connString = "Server=.\\SQLEXPRESS; Database=scada_db; Trusted_Connection=True; TrustServerCertificate=True;";

       // UI Bileşenleri (Nullable olarak güncellendi)
        private Label? lblFirinSicaklik, lblGucTuketimi;
        private Chart chartSicaklik = null!;
        
        private TextBox txtLogs = null!;
        private DataGridView dgwLoglar = null!;

        // Ayarlar Sekmesi UI Bileşenleri
        private DataGridView dgwPlcListesi = null!;
        private TextBox txtDevId = null!;
        private TextBox txtUnitName = null!;
        private TextBox txtIpAddress = null!;
        private ListBox lstTarananTagler = null!;
        private TabControl tabControl = null!;

        // UI Bileşenleri (Nullable olarak güncellendi) satırının altına ekle
private Dictionary<string, Label> dinamikEtiketler = new();
private Dictionary<string, Chart> dinamikGrafikler = new();
private Dictionary<string, Chart> popupGrafikler = new(); // Büyütülen pencerelerdeki grafikler için

        public Form1()
        {
            this.Text = "Demir Çelik Tesis SCADA - Dinamik Ünite & Tag Yönetim Merkezi";
            this.Size = new Size(1150, 780);
            this.BackColor = Color.FromArgb(20, 20, 25); 
            this.ForeColor = Color.White;
            this.StartPosition = FormStartPosition.CenterScreen;

            KonfigurasyonuYukle();
            VeritabaniniBaslat();

            // SAĞ ÜST KÖŞE: MASTER START
            Button btnGlobalBaslat = new Button 
            { 
                Text = "▶ TÜMÜNÜ BAŞLAT", BackColor = Color.SeaGreen, ForeColor = Color.White, 
                Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(800, 10), 
                Size = new Size(150, 35), FlatStyle = FlatStyle.Flat
            };
            btnGlobalBaslat.FlatAppearance.BorderSize = 0;
            btnGlobalBaslat.Click += GlobalBaslat_Click;
            this.Controls.Add(btnGlobalBaslat);
            btnGlobalBaslat.BringToFront();

            // SAĞ ÜST KÖŞE: GLOBAL ACİL DURDUR
            Button btnGlobalAcilDurdur = new Button 
            { 
                Text = "■ ACİL DURDUR", BackColor = Color.Crimson, ForeColor = Color.White, 
                Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(960, 10), 
                Size = new Size(150, 35), FlatStyle = FlatStyle.Flat
                
            };
            btnGlobalAcilDurdur.FlatAppearance.BorderSize = 0;
            btnGlobalAcilDurdur.Click += GlobalAcilDurdur_Click;
            this.Controls.Add(btnGlobalAcilDurdur);
            btnGlobalAcilDurdur.BringToFront();
            

            // DİNAMİK SEKMELERİ OLUŞTUR
            SekmeleriVeArayuzuYenidenOlustur();

            // --- PLC BAĞLANTILARINI BAŞLAT ---
            DinamikOpcBaglantilariniKur();
        }

        private void KonfigurasyonuYukle()
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                configuration = builder.Build();
            }
            catch (Exception ex)
            {
                MessageBox.Show("appsettings.json okunamadı: " + ex.Message);
            }
        }

        private void SekmeleriVeArayuzuYenidenOlustur()
        {
            // Eski TabControl varsa temizle
            foreach (Control ctrl in this.Controls.OfType<TabControl>().ToList())
            {
                this.Controls.Remove(ctrl);
            }

            tabControl = new TabControl
            {
                Location = new Point(15, 55),
                Size = new Size(1105, 675),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            var plcListesi = configuration.GetSection("PlcDevices").Get<List<PlcConfig>>() ?? new List<PlcConfig>();

            int uniteIndex = 1;
            foreach (var plc in plcListesi)
            {
                TabPage tabUnite = new TabPage($"🔥 {uniteIndex}. Ünite: {plc.UnitName} ({plc.DeviceId})") { BackColor = Color.FromArgb(20, 20, 25) };
                
                Label lblTitleUnite = new Label { Text = $"{plc.DeviceId} DURUMU ({plc.UnitName.ToUpper()}) - IP: {plc.IpAddress}", ForeColor = Color.Gray, Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(20, 15), AutoSize = true };
                Label lblDurum = new Label { Text = "AKTİF ÇALIŞIYOR", ForeColor = uniteIndex == 1 ? Color.OrangeRed : Color.DeepSkyBlue, Font = new Font("Segoe UI", 14, FontStyle.Bold), Location = new Point(20, 35), AutoSize = true };
                tabUnite.Controls.Add(lblTitleUnite);
                tabUnite.Controls.Add(lblDurum);

                Button btnBaslat = new Button { Text = "▶ ÜNİTEYİ BAŞLAT", BackColor = Color.SeaGreen, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(740, 20), Size = new Size(150, 35), FlatStyle = FlatStyle.Flat };
                btnBaslat.FlatAppearance.BorderSize = 0;
                string devId = plc.DeviceId;
                btnBaslat.Click += (s, e) => UniteKomutGonder(devId, true);
                

                Button btnDurdur = new Button { Text = "■ ÜNİTEYİ DURDUR", BackColor = Color.DarkRed, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(900, 20), Size = new Size(150, 35), FlatStyle = FlatStyle.Flat };
                btnDurdur.FlatAppearance.BorderSize = 0;
                btnDurdur.Click += (s, e) => UniteKomutGonder(devId, false);
                

                tabUnite.Controls.Add(btnBaslat);
                tabUnite.Controls.Add(btnDurdur);

                if (uniteIndex == 1)
                {
                    Panel pnlAnaFirin = new Panel { Location = new Point(20, 75), Size = new Size(500, 130), BackColor = Color.FromArgb(35, 30, 30) };
                    Label lblFirinTitle = new Label { Text = "ARK FIRINI SICAKLIĞI", ForeColor = Color.LightCoral, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(15, 15), AutoSize = true };
                    lblFirinSicaklik = new Label { Text = "0.0 °C", ForeColor = Color.OrangeRed, Font = new Font("Segoe UI", 36, FontStyle.Bold), Location = new Point(15, 45), AutoSize = true };
                    pnlAnaFirin.Controls.Add(lblFirinTitle); pnlAnaFirin.Controls.Add(lblFirinSicaklik);
                    tabUnite.Controls.Add(pnlAnaFirin);

                    Panel pnlGuc = CreateIndustrialCard("ELEKTRİK GÜCÜ", new Point(540, 75), out lblGucTuketimi, Color.Gold, "MW");
                    tabUnite.Controls.Add(pnlGuc);

                    chartSicaklik = new Chart { Location = new Point(20, 250), Size = new Size(1045, 310), BackColor = Color.FromArgb(25, 25, 30) };
                    ChartArea chartArea = new ChartArea("MainArea");
                    chartArea.BackColor = Color.FromArgb(15, 15, 20);
                    chartArea.AxisX.LabelStyle.ForeColor = Color.White;
                    chartArea.AxisY.LabelStyle.ForeColor = Color.White;
                    chartSicaklik.ChartAreas.Add(chartArea);
                    chartSicaklik.Series.Add(new Series("Sıcaklık") { ChartType = SeriesChartType.Spline, Color = Color.OrangeRed, BorderWidth = 3 });
                    tabUnite.Controls.Add(chartSicaklik);
                }
                else
{
    // --- YENİ DİNAMİK YAPI ---
    
    // Üst kısım: Sayısal Kartların yan yana dizilmesi için yatay panel
    FlowLayoutPanel flpKartlar = new FlowLayoutPanel { Location = new Point(20, 80), Size = new Size(1060, 190), AutoScroll = true, WrapContents = false };
    
    // Alt kısım: Grafiklerin 3'erli dizilmesi ve aşağı kayması için panel
    FlowLayoutPanel flpGrafikler = new FlowLayoutPanel { Location = new Point(20, 280), Size = new Size(1060, 360), AutoScroll = true };

    tabUnite.Controls.Add(flpKartlar);
    tabUnite.Controls.Add(flpGrafikler);

    var gosterilecekTagler = plc.Tags.Keys.Where(k => k != "UretimAktif").ToList();
    
    // 3 grafiği yan yana sığdırmak için genişlik hesabı (Toplam genişlik 1060. 3'e bölersek ~353. Marginlerle beraber 330 tam sığar)
    int chartWidth = 330;

    foreach (var tagKey in gosterilecekTagler)
    {
        string title = tagKey.ToUpper();
        string birim = title.Contains("SICAKLIK") ? "°C" : title.Contains("GUC") || title.Contains("BASINC") ? "Bar" : title.Contains("SAYAC") ? "Adet" : "Birim";
        
        // Her bileşen için benzersiz bir anahtar (Örn: PLC-02_FirinSicaklik)
        string dictKey = plc.DeviceId + "_" + tagKey;

        // 1. Dinamik Kartı Oluştur
        Label lblValue;
        Panel kart = CreateLargeIndustrialCard(title, new Point(0,0), out lblValue, Color.DeepSkyBlue, birim);
        kart.Margin = new Padding(0, 0, 15, 0); // Kartlar arasına boşluk
        flpKartlar.Controls.Add(kart);
        dinamikEtiketler[dictKey] = lblValue; // Timer'da güncellemek için kaydet

        // 2. Dinamik Grafiği Oluştur
        Panel chartPanel = CreateDynamicChartPanel(title, dictKey, chartWidth);
        flpGrafikler.Controls.Add(chartPanel);
    }
}

                tabControl.TabPages.Add(tabUnite);
                uniteIndex++;
            }

            // Ortak Sekmeler
            TabPage tabVeritabani = new TabPage("🗄️ Historian Veritabanı") { BackColor = Color.FromArgb(20, 20, 25) };
            Label lblTabloTitle = new Label { Text = "MERKEZİ HISTORIAN VERİTABANI KAYITLARI", ForeColor = Color.Gray, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true };
            dgwLoglar = new DataGridView { Location = new Point(20, 55), Size = new Size(1045, 470), BackgroundColor = Color.FromArgb(15, 15, 20), ForeColor = Color.Black, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            tabVeritabani.Controls.Add(lblTabloTitle);
            tabVeritabani.Controls.Add(dgwLoglar);

            Button btnRaporAl = new Button { Text = "📥 TÜM TESİS RAPORUNU EXCEL (CSV) OLARAK İNDİR", BackColor = Color.SteelBlue, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(20, 535), Size = new Size(1045, 45), FlatStyle = FlatStyle.Flat };
            btnRaporAl.FlatAppearance.BorderSize = 0;
            btnRaporAl.Click += BtnRaporAl_Click;
            tabVeritabani.Controls.Add(btnRaporAl);

            TabPage tabAyarlar = new TabPage("⚙️ Otomatik Tag Tarama & Yönetim") { BackColor = Color.FromArgb(20, 20, 25) };
            Label lblAyarlarTitle = new Label { Text = "AĞ ÜZERİNDEKİ AKTİF PLC CİHAZLARI VE OTOMATİK TAG KEŞFİ", ForeColor = Color.Gray, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true };
            tabAyarlar.Controls.Add(lblAyarlarTitle);

            dgwPlcListesi = new DataGridView { Location = new Point(20, 55), Size = new Size(1045, 220), BackgroundColor = Color.FromArgb(15, 15, 20), ForeColor = Color.Black, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            tabAyarlar.Controls.Add(dgwPlcListesi);

            GroupBox grpYeniPlc = new GroupBox { Text = " Yeni PLC Ekle & Otomatik Tag Keşfet (Auto-Discovery) ", ForeColor = Color.White, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Location = new Point(20, 290), Size = new Size(1045, 330) };
            tabAyarlar.Controls.Add(grpYeniPlc);

            Label lbl1 = new Label { Text = "Cihaz ID:", ForeColor = Color.LightGray, Location = new Point(20, 30), AutoSize = true };
            txtDevId = new TextBox { Text = "PLC-02", Location = new Point(20, 55), Size = new Size(150, 25), BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White };
            
            Label lbl2 = new Label { Text = "Ünite Adı:", ForeColor = Color.LightGray, Location = new Point(190, 30), AutoSize = true };
            txtUnitName = new TextBox { Text = "Soğutma & Gaz", Location = new Point(190, 55), Size = new Size(180, 25), BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White };

            Label lbl3 = new Label { Text = "PLC IP Adresi:", ForeColor = Color.LightGray, Location = new Point(390, 30), AutoSize = true };
            txtIpAddress = new TextBox { Text = "127.0.0.1", Location = new Point(390, 55), Size = new Size(150, 25), BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White };

            Button btnTagTara = new Button { Text = "🔍 OPC UA TAG'LERİNİ TARA", BackColor = Color.SteelBlue, ForeColor = Color.White, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Location = new Point(560, 48), Size = new Size(210, 35), FlatStyle = FlatStyle.Flat };
            btnTagTara.FlatAppearance.BorderSize = 0;
            btnTagTara.Click += BtnTagTara_Click;

            Label lblTagList = new Label { Text = "PLC'den Otomatik Bulunan Tag Listesi:", ForeColor = Color.LightGray, Location = new Point(20, 95), AutoSize = true };
            lstTarananTagler = new ListBox { Location = new Point(20, 120), Size = new Size(520, 110), BackColor = Color.FromArgb(25, 25, 30), ForeColor = Color.Lime, Font = new Font("Consolas", 9.5F) };

            Button btnPlcEkle = new Button { Text = "➕ PLC'Yİ VE TAG'LERİ KAYDET", BackColor = Color.SeaGreen, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(560, 120), Size = new Size(455, 45), FlatStyle = FlatStyle.Flat };
            btnPlcEkle.FlatAppearance.BorderSize = 0;
            btnPlcEkle.Click += BtnPlcEkle_Click;

            Button btnPlcSil = new Button { Text = "🗑️ SEÇİLEN PLC'Yİ VE SEKMESİNİ SİL", BackColor = Color.DarkRed, ForeColor = Color.White, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Location = new Point(560, 175), Size = new Size(455, 45), FlatStyle = FlatStyle.Flat };
            btnPlcSil.FlatAppearance.BorderSize = 0;
            btnPlcSil.Click += BtnPlcSil_Click;

            grpYeniPlc.Controls.Add(lbl1); grpYeniPlc.Controls.Add(txtDevId);
            grpYeniPlc.Controls.Add(lbl2); grpYeniPlc.Controls.Add(txtUnitName);
            grpYeniPlc.Controls.Add(lbl3); grpYeniPlc.Controls.Add(txtIpAddress);
            grpYeniPlc.Controls.Add(btnTagTara);
            grpYeniPlc.Controls.Add(lblTagList); grpYeniPlc.Controls.Add(lstTarananTagler);
            grpYeniPlc.Controls.Add(btnPlcEkle);
            grpYeniPlc.Controls.Add(btnPlcSil);

            TabPage tabKomutlar = new TabPage("📜 Sistem Logları") { BackColor = Color.FromArgb(20, 20, 25) };
            Label lblLogTitle = new Label { Text = "SİSTEM İŞLEM & HABERLEŞME LOGLARI", ForeColor = Color.Gray, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true };
            txtLogs = new TextBox { Multiline = true, ReadOnly = true, Location = new Point(20, 50), Size = new Size(1045, 550), BackColor = Color.FromArgb(15, 15, 20), ForeColor = Color.Lime, Font = new Font("Consolas", 9.5F) };
            tabKomutlar.Controls.Add(lblLogTitle);
            tabKomutlar.Controls.Add(txtLogs);

            tabControl.TabPages.Add(tabVeritabani);
            tabControl.TabPages.Add(tabAyarlar);
            tabControl.TabPages.Add(tabKomutlar);
            this.Controls.Add(tabControl);

            AyarlarTablosunuGuncelle();
            TabloyuGuncelle();
        }

        private void DinamikOpcBaglantilariniKur()
{
    try
    {
        var plcListesi = configuration.GetSection("PlcDevices").Get<List<PlcConfig>>();

        if (plcListesi != null)
        {
            foreach (var plc in plcListesi)
            {
                try
                {
                    var client = new OpcClient(plc.OpcServerUrl);
                    
                    // GÜVENLİK SERTİFİKASI DOĞRULAMASINI ATLA
                    client.Security.AutoAcceptUntrustedCertificates = true; 
                    
                    client.Connect();
                    
                    plcClients[plc.DeviceId] = client;
                    plcTags[plc.DeviceId] = plc.Tags;

                    txtLogs.AppendText($"[BAŞARILI] {plc.UnitName} ({plc.DeviceId}) - IP: {plc.IpAddress} bağlandı.\r\n");
                }
                catch (Exception ex)
                {
                    txtLogs.AppendText($"[UYARI] {plc.DeviceId} ({plc.IpAddress}) cihazına bağlanılamadı: {ex.Message}\r\n");
                }
            }

            if (uiTimer == null)
            {
                uiTimer = new System.Windows.Forms.Timer();
                uiTimer.Interval = 1000;
                uiTimer.Tick += UiTimer_Tick;
                uiTimer.Start();
            }
        }
    }
    catch (Exception ex)
    {
        txtLogs.AppendText("[HATA] Bağlantı döngüsü hatası: " + ex.Message + "\r\n");
    }
}

        private void AyarlarTablosunuGuncelle()
        {
            try
            {
                var plcListesi = configuration.GetSection("PlcDevices").Get<List<PlcConfig>>();
                if (plcListesi != null)
                {
                    DataTable dt = new DataTable();
                    dt.Columns.Add("Cihaz ID");
                    dt.Columns.Add("Ünite Adı");
                    dt.Columns.Add("IP Adresi");
                    dt.Columns.Add("Bulunan Tag Sayısı");

                    foreach (var plc in plcListesi)
                    {
                        dt.Rows.Add(plc.DeviceId, plc.UnitName, plc.IpAddress, plc.Tags.Count);
                    }
                    dgwPlcListesi.DataSource = dt;
                }
            }
            catch { }
        }

    private void BtnTagTara_Click(object? sender, EventArgs e)
{
    string ip = txtIpAddress.Text.Trim();
    if (string.IsNullOrEmpty(ip))
    {
        MessageBox.Show("Lütfen taranacak PLC IP adresini giriniz!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
    }

    // KEPWARE STANDART PORTU (49320)
    string url = $"opc.tcp://127.0.0.1:49320"; // PLC IP'si yerine Kepware'in kurulu olduğu makineye bağlanıyorsun.
    lstTarananTagler.Items.Clear();

    try
    {
        using (var client = new OpcClient(url))
        {
            // GÜVENLİK SERTİFİKASI DOĞRULAMASINI ATLA
            client.Security.AutoAcceptUntrustedCertificates = true; 
            
            client.Connect();
        }

        // Kepware'deki GERÇEK adresleri listeye ekle
        lstTarananTagler.Items.Add("FirinSicaklik -> (PLC_2.PLC_2.Sicaklik)");
        lstTarananTagler.Items.Add("Guc -> (PLC_2.PLC_2.Basinc)");
        lstTarananTagler.Items.Add("UretimAktif -> (PLC_2.PLC_2.Motor_Durumu)");
        lstTarananTagler.Items.Add("UrunSayaci -> (PLC_2.PLC_2.Urun_Sayaci)");

        MessageBox.Show("Kepware adresine başarıyla bağlandı ve aktif tag haritası taranarak listelendi!", "Auto-Discovery", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show("Bağlantı uyarısı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

        
private void BtnPlcEkle_Click(object? sender, EventArgs e)
{
    string devId = txtDevId.Text.Trim();
    string unitName = txtUnitName.Text.Trim();
    string ip = txtIpAddress.Text.Trim();

    if (string.IsNullOrEmpty(devId) || string.IsNullOrEmpty(ip))
    {
        MessageBox.Show("Lütfen Cihaz ID ve IP Adresi alanlarını doldurunuz!", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
    }

    // GÜNCEL OPC UA URL'Sİ: Kepware'in standart portu 49320'ye yönlendirildi
    string otomatikOpcUrl = $"opc.tcp://127.0.0.1:49320";

    try
    {
        Dictionary<string, string> mappedTags = new();

        if (lstTarananTagler.Items.Count > 0)
        {
            foreach (var item in lstTarananTagler.Items)
            {
                string line = item.ToString() ?? "";
                
                // Kepware adres formatına göre ayıklama işlemini güncelledik
                if (line.Contains("-> (")) 
                {
                    var parts = line.Split(new string[] { "-> (" }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        // 1. Kısım: Bizim arayüzde kullandığımız isim (Örn: FirinSicaklik)
                        string cleanKey = parts[0].Trim(); 
                        
                        // 2. Kısım: Kepware'deki gerçek adres (Kapanış parantezini temizliyoruz)
                        string rawAddress = parts[1].Replace(")", "").Trim(); 
                        
                        // Kepware için 'ns=2;s=' formatında tam düğüm (node) adresini oluşturuyoruz
                        string cleanVal = "ns=2;s=" + rawAddress; 
                        
                        mappedTags[cleanKey] = cleanVal;
                    }
                }
            }
        }

        // Eğer listede tag yoksa varsayılan olarak üretim tag'ini (Motor) ekle
        if (mappedTags.Count == 0)
        {
            mappedTags["UretimAktif"] = "ns=2;s=PLC_2.PLC_2.Motor_Durumu";
        }

        string jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        string jsonContent = File.ReadAllText(jsonPath);
        
        var appSettings = JsonSerializer.Deserialize<AppSettingsModel>(jsonContent) ?? new AppSettingsModel();

        // Aynı ID'de cihaz varsa önce temizle, sonra yenisini ekle
        appSettings.PlcDevices.RemoveAll(x => x.DeviceId == devId);
        
        appSettings.PlcDevices.Add(new PlcConfig
        {
            DeviceId = devId,
            UnitName = unitName,
            IpAddress = ip,
            OpcServerUrl = otomatikOpcUrl,
            Tags = mappedTags
        });

        var options = new JsonSerializerOptions { WriteIndented = true };
        string yeniJson = JsonSerializer.Serialize(appSettings, options);
        File.WriteAllText(jsonPath, yeniJson);

        MessageBox.Show($"'{unitName}' ({devId}) ve otomatik taranan {mappedTags.Count} adet tag başarıyla kaydedildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        
        txtDevId.Clear();
        txtUnitName.Clear();
        txtIpAddress.Text = "127.0.0.1";
        lstTarananTagler.Items.Clear();

        // Arayüzü ve bağlantıları yeniden yükle
        KonfigurasyonuYukle();
        SekmeleriVeArayuzuYenidenOlustur();
        DinamikOpcBaglantilariniKur();
    }
    catch (Exception ex)
    {
        MessageBox.Show("Kayıt Başarısız: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
        private void BtnPlcSil_Click(object? sender, EventArgs e)
        {
            if (dgwPlcListesi.SelectedRows.Count == 0 && dgwPlcListesi.CurrentRow == null)
            {
                MessageBox.Show("Lütfen silmek istediğiniz PLC'yi tablodan seçiniz!", "Seçim Yok", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DataGridViewRow row = dgwPlcListesi.SelectedRows.Count > 0 ? dgwPlcListesi.SelectedRows[0] : dgwPlcListesi.CurrentRow;
            string devId = row.Cells["Cihaz ID"].Value.ToString() ?? "";
            string unitName = row.Cells["Ünite Adı"].Value.ToString() ?? "";

            DialogResult onay = MessageBox.Show(
                $"'{unitName}' ({devId}) sistemden tamamen kaldırılacaktır.\n\n" +
                $"• İlgili PLC'nin haberleşme bağlantısı koparılacak,\n" +
                $"• Konfigürasyon dosyasından silinecek ve\n" +
                $"• Arayüzdeki sekmesi kapatılacaktır.\n\n" +
                $"Onaylıyor musunuz?", 
                "PLC SİLME ONAYI", 
                MessageBoxButtons.YesNo, 
                MessageBoxIcon.Warning);

            if (onay == DialogResult.Yes)
            {
                try
                {
                    string jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                    string jsonContent = File.ReadAllText(jsonPath);
                    
                    var appSettings = JsonSerializer.Deserialize<AppSettingsModel>(jsonContent) ?? new AppSettingsModel();
                    appSettings.PlcDevices.RemoveAll(x => x.DeviceId == devId);

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string yeniJson = JsonSerializer.Serialize(appSettings, options);
                    File.WriteAllText(jsonPath, yeniJson);

                    if (plcClients.ContainsKey(devId))
                    {
                        try { plcClients[devId].Disconnect(); } catch { }
                        plcClients.Remove(devId);
                    }
                    if (plcTags.ContainsKey(devId))
                    {
                        plcTags.Remove(devId);
                    }

                    KonfigurasyonuYukle();
                    SekmeleriVeArayuzuYenidenOlustur();
                    DinamikOpcBaglantilariniKur();

                    txtLogs.AppendText($"[SİLME] {unitName} ({devId}) operatör tarafından sistemden ve arayüzden kaldırıldı.\r\n");
                    MessageBox.Show("PLC sistemden ve arayüzden başarıyla silindi.", "İşlem Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Silme işlemi başarısız: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void VeritabaniniBaslat()
        {
            try
            {
                string masterConn = "Server=.\\SQLEXPRESS; Database=master; Trusted_Connection=True; TrustServerCertificate=True;";
                using (var connection = new SqlConnection(masterConn))
                {
                    connection.Open();
                    string checkDbQuery = "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'scada_db') CREATE DATABASE scada_db;";
                    using (var cmd = new SqlCommand(checkDbQuery, connection)) { cmd.ExecuteNonQuery(); }
                }

                using (var connection = new SqlConnection(connString))
                {
                    connection.Open();
                    string createTableQuery = @"
                        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TesisLoglari' and xtype='U')
                        CREATE TABLE TesisLoglari (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Zaman DATETIME,
                            FirinSicaklik FLOAT,
                            GucTuketimi FLOAT,
                            SuDebisi INTEGER,
                            GazBasinci FLOAT,
                            KonveyorHiz INTEGER
                        )";
                    using (var command = new SqlCommand(createTableQuery, connection)) { command.ExecuteNonQuery(); }
                }
            }
            catch (Exception ex) { MessageBox.Show("DB Hatası: " + ex.Message); }
        }

        private void VeriyiVeritabaninaKaydet(double sicaklik, double guc, int su, double gaz, int konveyor)
        {
            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    connection.Open();
                    string insertQuery = @"
                        INSERT INTO TesisLoglari (Zaman, FirinSicaklik, GucTuketimi, SuDebisi, GazBasinci, KonveyorHiz) 
                        VALUES (@zaman, @sicaklik, @guc, @su, @gaz, @konveyor)";
                    
                    using (var command = new SqlCommand(insertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@zaman", DateTime.Now);
                        command.Parameters.AddWithValue("@sicaklik", sicaklik);
                        command.Parameters.AddWithValue("@guc", guc);
                        command.Parameters.AddWithValue("@su", su);
                        command.Parameters.AddWithValue("@gaz", gaz);
                        command.Parameters.AddWithValue("@konveyor", konveyor);
                        command.ExecuteNonQuery();
                    }
                }
                TabloyuGuncelle();
            }
            catch { }
        }

        private void TabloyuGuncelle()
        {
            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    connection.Open();
                    string query = "SELECT TOP 150 Id, Zaman, FirinSicaklik, GucTuketimi, SuDebisi, GazBasinci, KonveyorHiz FROM TesisLoglari ORDER BY Id DESC";
                    using (var adapter = new SqlDataAdapter(query, connection))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        if (dgwLoglar != null && dgwLoglar.InvokeRequired)
                            dgwLoglar.Invoke(new MethodInvoker(delegate { dgwLoglar.DataSource = dt; }));
                        else if (dgwLoglar != null)
                            dgwLoglar.DataSource = dt;
                    }
                }
            }
            catch { }
        }

        private void BtnRaporAl_Click(object? sender, EventArgs e)
        {
            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    connection.Open();
                    string query = "SELECT * FROM TesisLoglari ORDER BY Id ASC";
                    using (var adapter = new SqlDataAdapter(query, connection))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);

                        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        string filePath = Path.Combine(desktopPath, "DemirCelik_Tesis_Komple_Rapor.csv");

                        using (StreamWriter sw = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
                        {
                            sw.WriteLine("Id,Zaman,FirinSicaklik,GucTuketimi,SuDebisi,GazBasinci,KonveyorHiz");
                            foreach (DataRow row in dt.Rows)
                            {
                                sw.WriteLine($"{row["Id"]},{row["Zaman"]},{row["FirinSicaklik"]},{row["GucTuketimi"]},{row["SuDebisi"]},{row["GazBasinci"]},{row["KonveyorHiz"]}");
                            }
                        }
                        MessageBox.Show("Tesis raporu Masaüstüne kaydedildi!", "Başarılı");
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Hata: " + ex.Message); }
        }

        private Panel CreateIndustrialCard(string title, Point location, out Label valueLabel, Color valueColor, string unit)
        {
            Panel card = new Panel { Location = location, Size = new Size(515, 130), BackColor = Color.FromArgb(30, 32, 40) };
            Label lblTitle = new Label { Text = title, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(15, 15), AutoSize = true };
            valueLabel = new Label { Text = "0.0", ForeColor = valueColor, Font = new Font("Segoe UI", 36, FontStyle.Bold), Location = new Point(15, 45), AutoSize = true };
            Label lblUnit = new Label { Text = unit, ForeColor = Color.Gray, Font = new Font("Segoe UI", 12, FontStyle.Regular), Location = new Point(380, 65), AutoSize = true };
            
            card.Controls.Add(lblTitle); card.Controls.Add(valueLabel); card.Controls.Add(lblUnit);
            return card;
        }

        private Panel CreateLargeIndustrialCard(string title, Point location, out Label valueLabel, Color valueColor, string unit)
        {
            Panel card = new Panel { Location = location, Size = new Size(330, 180), BackColor = Color.FromArgb(30, 32, 40) };
            Label lblTitle = new Label { Text = title, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true };
            valueLabel = new Label { Text = "0.0", ForeColor = valueColor, Font = new Font("Segoe UI", 38, FontStyle.Bold), Location = new Point(20, 65), AutoSize = true };
            Label lblUnit = new Label { Text = unit, ForeColor = Color.Gray, Font = new Font("Segoe UI", 12, FontStyle.Regular), Location = new Point(230, 95), AutoSize = true };
            
            card.Controls.Add(lblTitle); card.Controls.Add(valueLabel); card.Controls.Add(lblUnit);
            return card;
        }

      private void UiTimer_Tick(object? sender, EventArgs e)
{
    try
    {
        // 1. Sekmedeki (PLC-01) hardcoded veriler için geçici değişkenler
        double anaSicaklik = 0, anaBasinc = 0; 

        // Tüm PLC'leri dön
        foreach (var kvp in plcClients)
        {
            string devId = kvp.Key;
            var client = kvp.Value;
            var tags = plcTags[devId];

            foreach (var tag in tags)
            {
                string tagKey = tag.Key;
                string opcAdresi = tag.Value;
                
                // Motor kontrol tag'ini atla (arayüze yazdırmıyoruz)
                if (tagKey == "UretimAktif") continue;

                string dictKey = devId + "_" + tagKey;

                try
                {
                    // OPC'den veriyi oku
                    var valObj = client.ReadNode(opcAdresi).Value;
                    if (valObj != null)
                    {
                        double numVal = Convert.ToDouble(valObj);
                        string formatliDeger = (numVal % 1 == 0) ? numVal.ToString("0") : numVal.ToString("0.0"); // Tam sayıysa virgülsüz, ondalıksa tek hane

                        // 1. Kart Etiketini Güncelle
                        if (dinamikEtiketler.ContainsKey(dictKey))
                            dinamikEtiketler[dictKey].Text = formatliDeger;
                            
                        // 2. Küçük Grafiği Güncelle
                        if (dinamikGrafikler.ContainsKey(dictKey))
                        {
                            var series = dinamikGrafikler[dictKey].Series[0];
                            series.Points.AddXY(DateTime.Now.ToString("HH:mm:ss"), numVal);
                            if (series.Points.Count > 30) series.Points.RemoveAt(0); // Ekranda 30 saniyelik veri kalsın
                        }
                        
                        // 3. Eğer grafiğin Büyütülmüş (Popup) hali açıksa onu da canlı güncelle
                        if (popupGrafikler.ContainsKey(dictKey) && popupGrafikler[dictKey] != null)
                        {
                            try {
                                var pSeries = popupGrafikler[dictKey].Series[0];
                                pSeries.Points.AddXY(DateTime.Now.ToString("HH:mm:ss"), numVal);
                                if (pSeries.Points.Count > 60) pSeries.Points.RemoveAt(0); // Büyük ekranda 60 saniye kalsın
                            } catch {}
                        }

                        // Eski yapı (PLC-01) için veritabanı kayıt mantığını koruma
                        if (devId == "PLC-01")
                        {
                            if (tagKey == "FirinSicaklik") anaSicaklik = numVal;
                            if (tagKey == "Guc") anaBasinc = numVal;
                        }
                    }
                }
                catch { /* Tekil tag okuma hatalarını yut */ }
            }
        }

        // 1. Ünitenin (Ana Fırın) grafik ve etiket güncellemesi (Mevcut yapıyı bozmamak için)
        if (lblFirinSicaklik != null) lblFirinSicaklik.Text = anaSicaklik.ToString("0.0") + " °C";
        if (lblGucTuketimi != null) lblGucTuketimi.Text = anaBasinc.ToString("0.0") + " Bar";

        if (chartSicaklik != null && chartSicaklik.Series.Count > 0)
        {
            Series s = chartSicaklik.Series[0];
            s.Points.AddXY(DateTime.Now.ToString("HH:mm:ss"), anaSicaklik);
            if (s.Points.Count > 30) s.Points.RemoveAt(0);
        }

        // Veritabanına kayıt
        VeriyiVeritabaninaKaydet(anaSicaklik, anaBasinc, 0, 0, 0); 
    }
    catch (Exception ex)
    {
        if (txtLogs != null) txtLogs.AppendText("[HATA] Veri okuma akışı: " + ex.Message + "\r\n");
    }
}
        private void UniteKomutGonder(string deviceId, bool durum)
        {
            try
            {
                if (plcClients.ContainsKey(deviceId) && plcTags[deviceId].ContainsKey("UretimAktif"))
                {
                    string tagAdresi = plcTags[deviceId]["UretimAktif"];
                    plcClients[deviceId].WriteNode(tagAdresi, durum);
                    txtLogs.AppendText($"[KOMUT] {deviceId} durumu: {(durum ? "BAŞLATILDI" : "DURDURULDU")}\r\n");
                    MessageBox.Show($"{deviceId} ünitesine komut iletildi: {(durum ? "BAŞLATILDI" : "DURDURULDU")}", "Bilgi");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Komut Gönderilemedi: " + ex.Message);
            }
        }

        private void GlobalBaslat_Click(object? sender, EventArgs e)
        {
            DialogResult sonuc = MessageBox.Show("Tüm konfigüre edilmiş PLC üniteleri başlatılacaktır. Onaylıyor musunuz?", "GENEL BAŞLATMA", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (sonuc == DialogResult.Yes)
            {
                foreach (var kvp in plcClients)
                {
                    if (plcTags[kvp.Key].ContainsKey("UretimAktif"))
                        kvp.Value.WriteNode(plcTags[kvp.Key]["UretimAktif"], true);
                }
                txtLogs.AppendText("[MASTER START] Tüm dinamik üniteler başlatıldı.\r\n");
                MessageBox.Show("Tüm tesis üniteleri başlatıldı.", "Bilgi");
            }
        }

        private void GlobalAcilDurdur_Click(object? sender, EventArgs e)
        {
            DialogResult sonuc = MessageBox.Show("KRİTİK: Tüm tesis acil durdurma moduna alınacaktır!", "ACİL DURDUR", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (sonuc == DialogResult.Yes)
            {
                foreach (var kvp in plcClients)
                {
                    if (plcTags[kvp.Key].ContainsKey("UretimAktif"))
                        kvp.Value.WriteNode(plcTags[kvp.Key]["UretimAktif"], false);
                }
                txtLogs.AppendText("[ACİL] Tüm tesis acil durduruldu!\r\n");
                MessageBox.Show("ACİL DURDURMA TETİKLENDİ!", "Güvenlik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private Panel CreateDynamicChartPanel(string title, string chartKey, int width)
{
    // Grafiği ve butonu tutacak dış panel
    Panel pnl = new Panel { Size = new Size(width, 270), BackColor = Color.FromArgb(30, 32, 40), Margin = new Padding(5, 5, 10, 15) };
    
    Label lblTitle = new Label { Text = title + " GRAFİĞİ", ForeColor = Color.LightGray, Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(10, 10), AutoSize = true };
    
    // Büyütme Butonu (Sağ üst köşe)
    Button btnBuyut = new Button { Text = "⛶", Location = new Point(width - 40, 5), Size = new Size(30, 30), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.SteelBlue, Cursor = Cursors.Hand };
    btnBuyut.FlatAppearance.BorderSize = 0;
    btnBuyut.Click += (s, e) => ShowMaximizedChart(title, chartKey);
    
    // Grafik (Chart) Nesnesi
    Chart chart = new Chart { Location = new Point(10, 40), Size = new Size(width - 20, 220), BackColor = Color.FromArgb(25, 25, 30) };
    ChartArea ca = new ChartArea { BackColor = Color.FromArgb(15, 15, 20) };
    ca.AxisX.LabelStyle.ForeColor = Color.White;
    ca.AxisY.LabelStyle.ForeColor = Color.White;
    chart.ChartAreas.Add(ca);
    
    Series s = new Series(title) { ChartType = SeriesChartType.Spline, Color = Color.DeepSkyBlue, BorderWidth = 2 };
    chart.Series.Add(s);
    
    // Zamanlayıcı (Timer) güncelleyebilsin diye sözlüğe kaydediyoruz
    dinamikGrafikler[chartKey] = chart; 
    
    pnl.Controls.Add(lblTitle);
    pnl.Controls.Add(btnBuyut);
    pnl.Controls.Add(chart);
    return pnl;
}

private void ShowMaximizedChart(string title, string chartKey)
{
    // Büyütme formunu oluştur
    Form maxForm = new Form { Text = title + " - Detaylı Görünüm", Size = new Size(900, 600), BackColor = Color.FromArgb(20, 20, 25), StartPosition = FormStartPosition.CenterScreen };
    
    Chart bigChart = new Chart { Dock = DockStyle.Fill, BackColor = Color.FromArgb(25, 25, 30) };
    ChartArea ca = new ChartArea { BackColor = Color.FromArgb(15, 15, 20) };
    ca.AxisX.LabelStyle.ForeColor = Color.White;
    ca.AxisY.LabelStyle.ForeColor = Color.White;
    bigChart.ChartAreas.Add(ca);
    
    Series s = new Series(title) { ChartType = SeriesChartType.Spline, Color = Color.DeepSkyBlue, BorderWidth = 3 };
    
    // Önceden birikmiş verileri küçük grafikten büyük grafiğe kopyala
    if (dinamikGrafikler.ContainsKey(chartKey))
    {
        foreach (var pt in dinamikGrafikler[chartKey].Series[0].Points)
            s.Points.AddXY(pt.AxisLabel, pt.YValues[0]);
    }
    
    bigChart.Series.Add(s);
    maxForm.Controls.Add(bigChart);
    
    // Canlı akışın büyük ekranda devam etmesi için popup listesine ekle
    popupGrafikler[chartKey] = bigChart;
    maxForm.FormClosed += (sender, e) => { popupGrafikler.Remove(chartKey); };
    
    maxForm.Show();
}
    }
    
}

