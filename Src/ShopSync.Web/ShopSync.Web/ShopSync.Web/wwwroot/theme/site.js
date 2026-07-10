/* ShopSync Admin — Minimal JS (progressive enhancement) */

(function () {
  'use strict';

  /* Sidebar toggle (mobile) */
  var toggle = document.querySelector('.hamburger');
  var sidebar = document.querySelector('.sidebar');
  var overlay = document.querySelector('.sidebar-overlay');

  if (toggle && sidebar) {
    toggle.addEventListener('click', function () {
      sidebar.classList.toggle('open');
    });
  }

  if (overlay && sidebar) {
    overlay.addEventListener('click', function () {
      sidebar.classList.remove('open');
    });
  }

  /* Auto-dismiss alerts after 5s */
  var alerts = document.querySelectorAll('.alert[data-auto-dismiss]');
  alerts.forEach(function (el) {
    setTimeout(function () {
      el.style.transition = 'opacity 0.3s';
      el.style.opacity = '0';
      setTimeout(function () { el.remove(); }, 300);
    }, 5000);
  });

  /* Dynamic order item rows (order-create page) */
  var addRowBtn = document.getElementById('add-item-row');
  var itemRows = document.getElementById('item-rows');

  if (addRowBtn && itemRows) {
    addRowBtn.addEventListener('click', function () {
      var row = document.createElement('div');
      row.className = 'form-row mt-sm items-center';
      row.innerHTML =
        '<div class="form-group">' +
          '<input type="text" name="items[].sku" class="form-input mono" placeholder="SKU" required>' +
        '</div>' +
        '<div class="form-group">' +
          '<input type="number" name="items[].quantity" class="form-input" placeholder="Miktar" min="1" required>' +
        '</div>' +
        '<button type="button" class="btn btn-danger btn-sm remove-row">Sil</button>';
      itemRows.appendChild(row);
    });

    itemRows.addEventListener('click', function (e) {
      if (e.target.classList.contains('remove-row')) {
        var rows = itemRows.querySelectorAll('.form-row');
        if (rows.length > 1) {
          e.target.closest('.form-row').remove();
        }
      }
    });
  }
})();
