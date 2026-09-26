/**
 * Planet of Twins: bug report relay (game.md §27.4). Deploy steps: README.md next to this file.
 *
 * The game POSTs JSON {format "pot-relay/1", reportId, fileName, category, description, contact, summary, zipBase64}.
 * This script checks it and emails the zip to the account that owns the script, so no address is written here or in
 * the game. It answers JSON {ok, message, id}.
 *
 * Guards, because the URL is public (it sits in the game): a size cap, a format check, "is it really a zip", one email
 * per report id, and at most MAX_REPORTS_PER_HOUR emails an hour. If the URL is ever abused, make a NEW deployment
 * (new URL) and put that URL in the game's DiagnosticsConfig.
 */

const FORMAT = 'pot-relay/1';
const MAX_BODY_CHARS = 26 * 1024 * 1024;   // an 18 MB zip is 24 MB as base64, plus the text fields
const MAX_ZIP_BYTES = 18 * 1024 * 1024;    // Gmail takes 25 MB per email
const MAX_REPORTS_PER_HOUR = 30;           // Gmail's free quota is 100 emails a day
const DUPLICATE_SECONDS = 6 * 60 * 60;     // a retried report within 6 h is not emailed twice
const ID_PATTERN = /^R-[A-Z2-9]{6}$/;
const EMAIL_PATTERN = /^[^\s@<>()",;:]+@[^\s@<>()",;:]+\.[A-Za-z]{2,}$/;

function doPost(e) {
  try {
    if (!e || !e.postData || !e.postData.contents) return reply(false, 'empty request');
    const raw = e.postData.contents;
    if (raw.length > MAX_BODY_CHARS) return reply(false, 'report too big');

    let report;
    try { report = JSON.parse(raw); } catch (err) { return reply(false, 'not JSON'); }
    if (!report || report.format !== FORMAT) return reply(false, 'unknown format');
    const id = String(report.reportId || '');
    if (!ID_PATTERN.test(id)) return reply(false, 'bad report id');

    const cache = CacheService.getScriptCache();
    if (cache.get('sent:' + id)) return reply(true, 'already received', id);

    const bytes = Utilities.base64Decode(String(report.zipBase64 || ''));
    if (bytes.length < 4 || bytes[0] !== 0x50 || bytes[1] !== 0x4B) return reply(false, 'not a zip');   // "PK"
    if (bytes.length > MAX_ZIP_BYTES) return reply(false, 'report too big');

    if (!takeRateSlot(cache)) return reply(false, 'too many reports right now, please try again later');
    if (MailApp.getRemainingDailyQuota() < 1) return reply(false, 'the mail quota is used up for today');

    const mail = {
      to: Session.getEffectiveUser().getEmail(),
      subject: subjectFor(report, id),
      body: bodyFor(report, id),
      attachments: [Utilities.newBlob(bytes, 'application/zip', safeFileName(report.fileName, id))],
      name: 'Planet of Twins reports',
    };
    const contact = clip(report.contact, 120);
    if (EMAIL_PATTERN.test(contact)) mail.replyTo = contact;   // "Reply" in Gmail writes to the player
    MailApp.sendEmail(mail);

    cache.put('sent:' + id, '1', DUPLICATE_SECONDS);
    return reply(true, 'sent', id);
  } catch (err) {
    console.error(err);
    return reply(false, 'relay error: ' + describeError(err));
  }
}

/** The error's message for the game's log, with any email address blanked (the reply goes to whoever posted). */
function describeError(err) {
  const text = String((err && err.message) || err || 'unknown');
  return text.replace(/[^\s@<>()",;:]+@[^\s@<>()",;:]+/g, '<email>').slice(0, 200);
}

/** Health check for the game's editor tool: no email is sent. */
function doGet() {
  return reply(true, 'relay alive', '');
}

/** Run this once from the Apps Script editor: it asks for the mail permission and sends you a test email. */
function testMail() {
  askForAllPermissions();
  MailApp.sendEmail(Session.getEffectiveUser().getEmail(), '[PoT] relay test',
                    'The Planet of Twins report relay can send you email.');
}

/**
 * Run this from the Apps Script editor to test the whole relay without the game: it posts a tiny fake report to
 * doPost. The Execution log shows the reply, and the real error if there is one. On success you get a
 * "[PoT] R-TEST22" email.
 */
function testRelay() {
  askForAllPermissions();
  const zip = Utilities.zip([Utilities.newBlob('Relay test from the Apps Script editor.', 'text/plain', 'test.txt')],
                            'relay_test.zip');
  const report = {
    format: FORMAT,
    reportId: 'R-TEST22',
    fileName: 'relay_test.zip',
    category: 'Other',
    description: 'Relay test from the Apps Script editor (testRelay).',
    contact: '',
    summary: 'editor test',
    zipBase64: Utilities.base64Encode(zip.getBytes()),
  };
  CacheService.getScriptCache().remove('sent:' + report.reportId);   // so the test can be repeated
  const answer = doPost({ postData: { contents: JSON.stringify(report) } });
  console.log('Reply: ' + answer.getContent());
}

/**
 * Google's consent screen lets you untick single permissions, and an unticked "Send email as you" makes every report
 * fail. From the editor, this stops the run and shows the consent screen again until every permission is granted.
 */
function askForAllPermissions() {
  if (typeof ScriptApp.requireAllScopes === 'function') ScriptApp.requireAllScopes(ScriptApp.AuthMode.FULL);
}

function takeRateSlot(cache) {
  const lock = LockService.getScriptLock();
  if (!lock.tryLock(5000)) return false;
  try {
    const key = 'count:' + Utilities.formatDate(new Date(), 'UTC', 'yyyyMMddHH');
    const count = Number(cache.get(key) || 0);
    if (count >= MAX_REPORTS_PER_HOUR) return false;
    cache.put(key, String(count + 1), 3600 + 300);
    return true;
  } finally {
    lock.releaseLock();
  }
}

function subjectFor(report, id) {
  const firstLine = clip(String(report.description || '').split('\n')[0], 60);
  const category = clip(report.category, 20);
  return '[PoT] ' + id + (category ? ' · ' + category : '') + (firstLine ? ' · ' + firstLine : '');
}

function bodyFor(report, id) {
  return [
    'Report ' + id,
    'Type: ' + (clip(report.category, 20) || '(none)'),
    'Contact: ' + (clip(report.contact, 120) || '(none)'),
    'Build: ' + (clip(report.summary, 300) || '(unknown)'),
    'Received: ' + new Date().toISOString(),
    '',
    'What happened:',
    String(report.description || '(no description)').slice(0, 4000),
    '',
    'The zip is attached: report.json, logs/ (with problems.txt), save/, the screenshot and any crash data.',
  ].join('\n');
}

function safeFileName(name, id) {
  const clean = String(name || '').replace(/[^A-Za-z0-9_.-]/g, '');
  return /\.zip$/i.test(clean) && clean.length <= 80 ? clean : 'report_' + id + '.zip';
}

function clip(value, max) {
  const text = String(value || '').replace(/[\r\n]+/g, ' ').trim();
  return text.length <= max ? text : text.slice(0, max) + '…';
}

function reply(ok, message, id) {
  return ContentService.createTextOutput(JSON.stringify({ ok: ok, message: message, id: id || '' }))
    .setMimeType(ContentService.MimeType.JSON);
}
