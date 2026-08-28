const latestDownload =
  "https://github.com/alphasixtyfive/resodrive/releases/latest/download/ResoDrive-Setup.exe";

const softwareApplicationData = {
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  name: "ResoDrive",
  url: "https://alphasixtyfive.github.io/resodrive/",
  description:
    "A free and open source Windows app for mounting Nextcloud, WebDAV and SFTP as drives, with copy and sync jobs.",
  applicationCategory: "UtilitiesApplication",
  operatingSystem: "64-bit Windows",
  isAccessibleForFree: true,
  license: "https://github.com/alphasixtyfive/resodrive/blob/main/LICENSE",
  downloadUrl: latestDownload,
  offers: {
    "@type": "Offer",
    price: "0",
    priceCurrency: "USD",
  },
};

const profiles = [
  {
    label: "Nextcloud",
    note: "A company Nextcloud connection with a suggested drive letter.",
    code: `{
  "id": "company-nextcloud",
  "displayName": "Company Nextcloud",
  "description": "Company files and personal folders",
  "defaultRemoteName": "Nextcloud",
  "connection": {
    "type": "webdav",
    "baseUrl": "https://cloud.example.com/",
    "pathTemplate": "/remote.php/dav/files/{username}",
    "vendor": "nextcloud"
  },
  "defaultDriveLetter": "N"
}`,
  },
  {
    label: "WebDAV",
    note: "A shared WebDAV location for team documents.",
    code: `{
  "id": "team-webdav",
  "displayName": "Team documents",
  "description": "Shared team documents",
  "defaultRemoteName": "Documents",
  "connection": {
    "type": "webdav",
    "baseUrl": "https://files.example.com/",
    "pathTemplate": "/dav/",
    "vendor": "other"
  },
  "defaultRemotePath": "shared",
  "defaultDriveLetter": "W"
}`,
  },
  {
    label: "SFTP",
    note: "An SFTP server that users can open as a normal drive.",
    code: `{
  "id": "project-sftp",
  "displayName": "Project server",
  "description": "Project files over SFTP",
  "defaultRemoteName": "Projects",
  "connection": {
    "type": "sftpPassword",
    "host": "sftp.example.com",
    "port": 22
  },
  "defaultRemotePath": "/projects",
  "defaultDriveLetter": "P"
}`,
  },
];

export default function Home() {
  return (
    <main id="top">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationData) }}
      />
      <header className="site-header">
        <a className="brand" href="#top" aria-label="ResoDrive home">
          <img src="/resodrive-mark.png" alt="" />
          <span>ResoDrive</span>
        </a>
        <nav aria-label="Main navigation">
          <a href="#features">Features</a>
          <a href="#teams">For teams</a>
          <a href="#profiles">Profiles</a>
          <a href="https://github.com/alphasixtyfive/resodrive">GitHub</a>
          <a className="nav-download" href={latestDownload}>Download</a>
        </nav>
      </header>

      <section className="hero section">
        <div className="hero-copy">
          <p className="section-label">Completely free and open source</p>
          <h1>Your cloud files, at home in Windows</h1>
          <p className="hero-intro">
            Mount Nextcloud, WebDAV and SFTP in Explorer. ResoDrive keeps drives
            connected and handles copy or sync jobs in the background.
          </p>
          <div className="protocol-list" aria-label="Supported connection types">
            <span>Nextcloud</span>
            <span>WebDAV</span>
            <span>SFTP</span>
          </div>
          <div className="hero-actions">
            <a className="button button-primary" href={latestDownload}>Download for Windows</a>
            <a className="button button-secondary" href="https://github.com/alphasixtyfive/resodrive">View source</a>
          </div>
          <p className="small-print">No subscription. No paid edition. MIT licensed for 64-bit Windows.</p>
        </div>
        <div className="screenshot-crop wide-shot hero-shot">
          <img
            className="product-shot"
            src="/screenshots/resodrive-drives.png"
            alt="ResoDrive showing a WebDAV drive and an SFTP drive"
          />
        </div>
      </section>

      <section className="section" id="features">
        <div className="section-heading">
          <p className="section-label">Made for everyday work</p>
          <h2>Remote storage that feels at home on Windows</h2>
        </div>
        <div className="feature-grid">
          <article>
            <h3>Drives in Explorer</h3>
            <p>Choose a drive letter and open remote files from Explorer or desktop apps.</p>
          </article>
          <article>
            <h3>Guided setup</h3>
            <p>Add Nextcloud, WebDAV or SFTP without editing rclone files by hand.</p>
          </article>
          <article>
            <h3>Background reconnects</h3>
            <p>Keep selected drives available after sign-in and retry brief connection failures.</p>
          </article>
          <article>
            <h3>Local credentials</h3>
            <p>Connection secrets are protected for the current Windows account.</p>
          </article>
        </div>

        <div className="split-section sync-section">
        <div className="split-copy">
          <p className="section-label">Copy and sync jobs</p>
          <h2>Copy and sync without keeping a drive open</h2>
          <p>
            Copy files in either direction or mirror a source to its destination. Jobs can
            run manually or on an interval, even when the related drive is not mounted.
          </p>
          <ul>
            <li>See progress and transferred data</li>
            <li>Review checks, errors and completion time</li>
            <li>Confirm mirror jobs before removing destination files</li>
          </ul>
        </div>
        <div className="screenshot-crop wide-shot">
          <img
            className="product-shot"
            src="/screenshots/resodrive-sync.png"
            alt="ResoDrive copy and sync jobs with recent results"
          />
        </div>
        </div>
      </section>

      <section className="section" id="teams">
        <div className="section-heading wide-heading">
          <p className="section-label">For Windows teams</p>
          <h2>A practical rollout for Windows teams</h2>
          <p>
            ResoDrive does not need a management server. IT can deploy the installer and
            provide connection profiles. Each user enters their own credentials.
          </p>
        </div>
        <ol className="rollout-grid">
          <li>
            <span>1</span>
            <div>
              <h3>Install ResoDrive</h3>
              <p>Deploy the MSI with your normal Windows software tool, or use the setup program for individual computers.</p>
            </div>
          </li>
          <li>
            <span>2</span>
            <div>
              <h3>Add profiles</h3>
              <p>Put <code>profiles.json</code> in <code>%LOCALAPPDATA%\rdrive</code>. Profiles contain addresses and defaults, not passwords.</p>
            </div>
          </li>
          <li>
            <span>3</span>
            <div>
              <h3>Let users connect</h3>
              <p>Users select a profile, enter their credentials and confirm the drive letter.</p>
            </div>
          </li>
        </ol>
        <p className="plain-note">
          This works well for teams that want repeatable setup without another admin
          service. ResoDrive does not currently have a central console or device reports.
        </p>

        <div className="profiles-section" id="profiles">
        <div className="profiles-inner">
          <div className="section-heading wide-heading">
            <p className="section-label">Profile examples</p>
            <h2>Give people a short, familiar setup</h2>
            <p>
              Profiles make setup shorter and more consistent. Users still provide their
              own password or private key.
            </p>
          </div>
          <div className="profile-grid">
            {profiles.map((profile) => (
              <article className="profile-card" key={profile.label}>
                <header>
                  <span className="profile-kind">{profile.label}</span>
                  <h3>{profile.label} profile</h3>
                  <p>{profile.note}</p>
                </header>
                <pre tabIndex={0} aria-label={`${profile.label} profile example`}><code>{profile.code}</code></pre>
              </article>
            ))}
          </div>
          <p className="profile-caption">
            These are individual profile objects. The complete file adds a schema version
            and puts the objects inside a <code>profiles</code> array.
          </p>
        </div>
        </div>
      </section>

      <section className="section clarity-section">
        <div className="split-section setup-section">
        <div className="screenshot-crop setup-shot">
          <img
            className="product-shot"
            src="/screenshots/resodrive-connection-setup.png"
            alt="ResoDrive Nextcloud connection setup"
          />
        </div>
        <div className="split-copy">
          <p className="section-label">First connection</p>
          <h2>Everything is clear before connecting</h2>
          <p>
            The setup screen shows the service address, remote path and drive letter
            before the connection is saved.
          </p>
          <a className="text-link" href="https://github.com/alphasixtyfive/resodrive#install">Read the install guide</a>
        </div>
        </div>

        <div className="trust-section">
        <div>
          <h3>No ResoDrive cloud</h3>
          <p>The app connects the computer directly to the configured storage service.</p>
        </div>
        <div>
          <h3>Protected credentials</h3>
          <p>Managed secrets are encrypted for the current Windows account.</p>
        </div>
        <div>
          <h3>Public source and checksums</h3>
          <p>Code, release history and SHA-256 checksums are available on GitHub.</p>
        </div>
        </div>
      </section>

      <section className="download-section">
        <img src="/resodrive-mark.png" alt="" />
        <div>
          <h2>Free to use. Open source.</h2>
          <p>Download the current preview for 64-bit Windows.</p>
        </div>
        <a className="button button-primary" href={latestDownload}>Download for Windows</a>
      </section>

      <footer>
        <a className="brand footer-brand" href="#top">
          <img src="/resodrive-mark.png" alt="" />
          <span>ResoDrive</span>
        </a>
        <nav aria-label="Footer navigation">
          <a href="https://github.com/alphasixtyfive/resodrive/releases">Releases</a>
          <a href="https://github.com/alphasixtyfive/resodrive/blob/main/CHANGELOG.md">Changelog</a>
          <a href="https://github.com/alphasixtyfive/resodrive/issues">Issues</a>
          <a href="https://github.com/alphasixtyfive/resodrive/blob/main/LICENSE">License</a>
        </nav>
      </footer>
    </main>
  );
}
