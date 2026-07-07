import http from 'k6/http';
import { check, sleep } from 'k6';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

export const options = { vus: 1, iterations: 1 };
const BASE_URL = 'http://localhost:5160';

export default function () {
  const payload = JSON.stringify({
    idempotencyKey: uuidv4(), 
    customerId: 'idemp-user', 
    items: [{ sku: 'SKU-001', quantity: 1 }] 
  });
  
  const params = { headers: { 'Content-Type': 'application/json' } };
  let firstOrderId = "";

  for (let i = 0; i < 5; i++) {
    const res = http.post(`${BASE_URL}/api/Order`, payload, params);
    
    // API'ın gerçekte ne döndüğünü (200 OK mu, 400 Hata mı) konsola yazdırıyoruz:
    console.log(`İstek ${i+1} | Status: ${res.status} | Body: ${res.body}`);
    
    if (i === 0) {
      firstOrderId = res.json('id') || res.json('orderId');
    } else {
      check(res, { 'Idempotency çalışıyor': () => (res.json('id') || res.json('orderId')) === firstOrderId });
    }
    
    sleep(0.5); 
  }
}