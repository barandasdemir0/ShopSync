const warehouses = ["DEFAULT", "WH-IST", "WH-ANK", "WH-IZM"];
const documents = [];

for (let i = 1; i <= 50; i++) {
    const sku = "SKU-" + i.toString().padStart(3, "0");
    
    for (const warehouse of warehouses) {
        // Rastgele 10 ile 500 arası stok belirle
        const qty = Math.floor(Math.random() * 490) + 10;
        
        documents.push({
            sku: sku,
            quantityAvailable: qty,
            quantityReserved: 0,
            warehouseCode: warehouse,
            lowStockThreshold: 20, // Eşik değeri 20 olsun
            createdAt: new Date(),
            updatedAt: new Date()
        });
    }
}

// Koleksiyonu temizle (eski test verileri çakışmasın diye)
db.inventory_items.deleteMany({});

// Yeni verileri ekle
const result = db.inventory_items.insertMany(documents);

print("Basariyla eklendi! Toplam eklenen kayit sayisi: " + result.insertedIds.length);
