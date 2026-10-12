"""
Fires N simultaneous order requests for one product and counts the statuses.

Usage (stdlib only, no pip install):
    python scripts/concurrency_test.py --email c@b.com --password password123 --product-id 1

Set the product's stock to a known number first (e.g. 5). With 20 requests of
quantity 1 you should see exactly 5 x 201 and 15 x 400, and stock must end at 0.
"""
import argparse
import json
import threading
import urllib.error
import urllib.request
from collections import Counter


def post_json(url, payload, token=None):
    data = json.dumps(payload).encode()
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    req = urllib.request.Request(url, data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return resp.status, resp.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--base-url", default="http://localhost:5221")
    p.add_argument("--email", required=True)
    p.add_argument("--password", required=True)
    p.add_argument("--product-id", type=int, required=True)
    p.add_argument("--requests", type=int, default=20)
    p.add_argument("--quantity", type=int, default=1)
    args = p.parse_args()

    status, body = post_json(
        f"{args.base_url}/auth/login",
        {"email": args.email, "password": args.password},
    )
    if status != 200:
        raise SystemExit(f"Login failed: {status} {body}")
    token = json.loads(body)["token"]

    order = {"items": [{"productId": args.product_id, "quantity": args.quantity}]}
    barrier = threading.Barrier(args.requests)   # all threads release at the same instant
    results, samples = [], {}
    lock = threading.Lock()

    def worker():
        barrier.wait()
        status, body = post_json(f"{args.base_url}/orders", order, token)
        with lock:
            results.append(status)
            samples.setdefault(status, body[:200])

    threads = [threading.Thread(target=worker) for _ in range(args.requests)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()

    print(f"\n{args.requests} simultaneous requests, quantity {args.quantity} each\n")
    for status, count in sorted(Counter(results).items()):
        print(f"  {status}: {count}   e.g. {samples[status]}")
    print("\nNow check the DB: Stock must be >= 0, and SUM(OrderItems.Quantity) "
          "for this product must equal the starting stock.")


if __name__ == "__main__":
    main()
