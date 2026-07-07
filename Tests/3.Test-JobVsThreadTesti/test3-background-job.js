import http from 'k6/http';
import { check, sleep } from 'k6';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

export const options = { 
  vus: 10,        // 10 Sanal Kullanıcı
  duration: '30s' // Tam 30 saniye boyunca hiç durmadan saldıracaklar
};

const BASE_URL = 'http://localhost:5160';

export default function () {
  const payload = JSON.stringify({
    idempotencyKey: uuidv4(),
    customerId: `stress-user-${__VU}`, 
    items: [{ sku: 'SKU-001', quantity: 1 }]
  });
  
  const params = { headers: { 'Content-Type': 'application/json' } }; 
  
  const res = http.post(`${BASE_URL}/api/Order`, payload, params);
  
  check(res, { 'Sistem çökmedi (500 Internal Error yok)': (r) => r.status !== 500 });
  
  // İstekler arası çok kısa bir bekleme (Sistemi tamamen boğmamak için)
  sleep(0.1);
}