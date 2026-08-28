# ResoDrive website

Local working version of the ResoDrive product website. It is intentionally not
connected to a public deployment yet.

## Preview

Requires Node.js 22.13 or newer.

```powershell
npm install
npm run dev
```

Open `http://localhost:3000/`.

## Check before publishing

```powershell
npm test
```

The main page is in `app/page.tsx`, its styling is in `app/globals.css`, and the
reused application screenshots are under `public/screenshots/`.
