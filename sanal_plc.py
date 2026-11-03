import time
import random
from opcua import Server, ua

# Sunucu Tanımlamaları
server = Server()
server.set_endpoint("opc.tcp://127.0.0.1:4841/freeopcua/server/")

uri = "http://demircelik.local"
idx = server.register_namespace(uri) 
plc = server.get_objects_node().add_object(idx, "DemirCelikPLC")

# C#'tan kontrol edeceğimiz "Üretim Durumu" değişkeni (Boolean)
uretim_aktif = plc.add_variable("ns=2;s=UretimAktif", "UretimAktif", ua.Variant(True, ua.VariantType.Boolean))

# Tesis Sensör Değişkenleri
sicaklik = plc.add_variable("ns=2;s=FirinSicaklik", "FirinSicaklik", ua.Variant(1600.0, ua.VariantType.Double))
guc = plc.add_variable("ns=2;s=Guc", "GucTuketimi", ua.Variant(45.0, ua.VariantType.Double))
su = plc.add_variable("ns=2;s=Su", "SuDebisi", ua.Variant(1200, ua.VariantType.Int32))
gaz = plc.add_variable("ns=2;s=Gaz", "GazBasinci", ua.Variant(12.0, ua.VariantType.Double))
konveyor = plc.add_variable("ns=2;s=Konveyor", "KonveyorHiz", ua.Variant(1450, ua.VariantType.Int32))

# Tüm değişkenleri C#'tan yazılabilir (komut alabilir) yapıyoruz
for var in [uretim_aktif, sicaklik, guc, su, gaz, konveyor]:
    var.set_writable()

# Sunucuyu Başlat
server.start()
print("ÇİFT YÖNLÜ İLETİŞİM AKTİF! (Port: 4841)")

try:
    while True:
        # C# arayüzünden bu değer 'True' olarak bırakıldığı sürece sistem çalışır ve veri üretir
        if uretim_aktif.get_value() == True:
            sicaklik.set_value(ua.Variant(1600.0 + random.uniform(0, 50), ua.VariantType.Double))
            guc.set_value(ua.Variant(45.0 + random.uniform(0, 10), ua.VariantType.Double))
            su.set_value(ua.Variant(random.randint(1200, 1250), ua.VariantType.Int32))
            gaz.set_value(ua.Variant(12.0 + random.uniform(0, 2), ua.VariantType.Double))
            konveyor.set_value(ua.Variant(random.randint(1450, 1480), ua.VariantType.Int32))
        else:
            # C# arayüzünden 'ACİL DURDUR' (False) komutu gelirse güç ve motor hızını 0'a çek
            guc.set_value(ua.Variant(0.0, ua.VariantType.Double))
            konveyor.set_value(ua.Variant(0, ua.VariantType.Int32))
            
        time.sleep(1)
finally:
    server.stop()