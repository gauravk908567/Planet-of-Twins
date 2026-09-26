# Planet of Twins: bug report relay

A small Google Apps Script that receives a bug report from the game and emails the zip to you. It runs on your own
Google account, for free: no server, no domain, and no password or key inside the game. Spec: game.md §27.4.

## Deploy (about 10 minutes, once)

1. Open <https://script.google.com> with the Google account that should receive the reports, and click **New project**.
   Rename it (top left) to **PoT Report Relay**.
2. Delete the sample code, paste all of [`Code.gs`](Code.gs), and save (Ctrl+S).
3. Pick **testMail** in the function list at the top, then click **Run**. Google asks for permission:
   - **Review permissions** → pick your account;
   - on "Google hasn't verified this app", click **Advanced** → **Go to PoT Report Relay (unsafe)**. It's your own
     script;
   - **Allow**.
   You should get an email "[PoT] relay test".
4. Click **Deploy** → **New deployment**. Next to "Select type", click the gear → **Web app**. Then:
   - Description: `v1`;
   - Execute as: **Me**;
   - Who has access: **Anyone**. Not "Anyone with a Google account": players aren't signed in to Google.
   Click **Deploy**, and copy the **Web app URL**. It ends in `/exec`.
5. In Unity, select `Assets/_PoT/Data/Diagnostics/DiagnosticsConfig.asset` and paste the URL into **Relay Url**.
6. Check the setup in Unity with *Planet of Twins Tools ▸ Diagnostics*:
   - **Relay: Check Deployment (no email)** → the Console says "relay alive";
   - **Relay: Send Test Report** → the Console shows each step, and a "[PoT] R-…" email with the zip arrives.

## If "Run" says "You do not have permission to call MailApp.sendEmail"

This happens even after you clicked Allow, for two known reasons:

- **Several Google accounts are signed in.** The permission window opens under your *default* account, which can't see
  the script, so it shows "Sorry, unable to open the file at present". Use a private window (Ctrl+Shift+N) signed in
  with ONLY the account that owns the script, for testMail and for the deploy.
- **A permission checkbox was left empty.** The consent screen lists each permission with its own checkbox. Tick
  **Select all** (or every box: "Send email as you" and "See your primary email address") before **Allow**.

Still failing: open <https://myaccount.google.com/permissions> in that account, remove the entry with your project's
name (e.g. **PoT Report Relay** or **Planet of Twins Project**), and run testMail again.

## If the game says "the server refused it: relay error: …"

The report reached the script, and the script failed. The text after "relay error:" is the script's own error. To see
it with more detail, pick **testRelay** in the editor and click **Run**. It sends a tiny fake report through the same
code, and the Execution log shows the reply and the error. Past runs, including the game's, are listed under
**Executions** (the list icon on the left).

The script never needs Drive sharing. Keep the project's Drive access **Restricted**: the game reaches the script
through the deployment's "Who has access: Anyone", which is a separate setting.

## Changing the script later

Edit the code, then **Deploy → Manage deployments** → the pencil icon → Version: **New version** → **Deploy**. The URL
stays the same. A **New deployment** would make a new URL, which only makes sense if the old URL is being abused.

## Limits and guards

- Gmail (free account): 100 emails a day, 25 MB per email. The relay allows 30 reports an hour, and the game doesn't
  send zips over 18 MB (those stay on the player's PC for "Email It to Us").
- Every request is checked: the size, the format name (`pot-relay/1`), the report id pattern, and that the file really
  is a zip. A report id already received in the last 6 hours isn't emailed again.
- The URL is in the game, so anyone could find it. The worst they can do is send junk to your inbox, within the limits
  above. If that happens, make a new deployment and put the new URL in the game.
- The email's **Reply** goes to the player when they left a valid email address.
