from playwright.sync_api import sync_playwright
import os
from dotenv import load_dotenv

load_dotenv()

with sync_playwright() as p:
    wss_url = os.getenv("BROWSERLESS_WSS")

    browser = p.chromium.connect(wss_url)
    context = browser.new_context()
    page = context.new_page()
    page.goto("http://www.example.com", wait_until="domcontentloaded")
    print(page.content())
    context.close()
