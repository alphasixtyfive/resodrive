import type { Metadata } from "next";
import "./globals.css";

const siteUrl = "https://alphasixtyfive.github.io/resodrive/";

export const metadata: Metadata = {
  metadataBase: new URL(siteUrl),
  title: "ResoDrive: Free Nextcloud, WebDAV and SFTP Drives for Windows",
  description:
    "Free and open source Windows app for mounting Nextcloud, WebDAV and SFTP as drives, with automatic reconnects and scheduled copy and sync jobs.",
  applicationName: "ResoDrive",
  keywords: [
    "Nextcloud Windows drive",
    "WebDAV Windows drive",
    "SFTP Windows drive",
    "open source drive mounting",
    "Windows sync app",
  ],
  alternates: { canonical: siteUrl },
  robots: { index: true, follow: true },
  category: "technology",
  icons: {
    icon: `${siteUrl}resodrive-mark.png`,
    shortcut: `${siteUrl}resodrive-mark.png`,
  },
  openGraph: {
    title: "ResoDrive: Free and Open Source Windows Drives",
    description: "Mount Nextcloud, WebDAV and SFTP as Windows drives for free.",
    type: "website",
    url: siteUrl,
    siteName: "ResoDrive",
    locale: "en_US",
  },
  twitter: {
    card: "summary",
    title: "ResoDrive: Free and Open Source Windows Drives",
    description: "Mount Nextcloud, WebDAV and SFTP as Windows drives for free.",
  },
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        <link
          href="https://fonts.googleapis.com/css2?family=Instrument+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500;600&display=swap"
          rel="stylesheet"
        />
      </head>
      <body>{children}</body>
    </html>
  );
}
