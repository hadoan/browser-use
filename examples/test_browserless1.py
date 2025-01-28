import asyncio
from playwright.async_api import async_playwright


async def wait_for_captcha(cdp_session):
    # Wait for the "Browserless.captchaFound" event
    future = asyncio.Future()

    def handle_captcha_found(event):
        print("Captcha found!")
        future.set_result(event)

    cdp_session.on("Browserless.captchaFound", handle_captcha_found)
    return await future


async def main():
    pw_endpoint = "wss://production-sfo.browserless.io/chromium?token="
    async with async_playwright() as p:
        try:
            # Connect to the browser
            browser = await p.chromium.connect_over_cdp(pw_endpoint)

            # Use the first context and page
            context = browser.contexts[0]
            page = context.pages[0]

            # Navigate to the captcha demo page
            await page.goto(
                "https://www.google.com/recaptcha/api2/demo", wait_until="networkidle"
            )

            # Create a CDP session
            cdp = await page.context.new_cdp_session(page)

            # Wait for captcha to be found
            await wait_for_captcha(cdp)

            # Solve the captcha
            result = await cdp.send("Browserless.solveCaptcha")
            solved, error = result.get("solved"), result.get("error")
            print({"solved": solved, "error": error})

            # Continue after solving captcha
            await page.click("#recaptcha-demo-submit")
            await browser.close()
        except Exception as e:
            print("There was a big error :(", e)


asyncio.run(main())
