// wwwroot/js/order-create.js

let rowCount = 1;

function addRow() {
    const container = document.getElementById('item-rows');
    const row = document.createElement('div');
    row.className = 'form-row flex items-center gap-sm mb-sm item-row';

    row.innerHTML = `
        <input type="hidden" name="Items.Index" value="${rowCount}" />
        <input type="text" name="Items[${rowCount}].Sku" class="form-input" placeholder="Ürün SKU" required />
        <input type="number" name="Items[${rowCount}].Quantity" class="form-input" placeholder="Miktar" required min="1" style="width: 120px;" />
        <button type="button" class="btn btn-danger btn-sm" onclick="this.parentElement.remove()">Sil</button>
    `;

    container.appendChild(row);
    rowCount++;
}