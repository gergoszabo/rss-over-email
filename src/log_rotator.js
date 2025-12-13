import fs from 'fs';
import path from 'path';
import nodemailer from 'nodemailer';

// Configuration for email - ideally these would come from environment variables
const EMAIL_SERVICE = process.env.EMAIL_SERVICE || 'gmail';
const EMAIL_USER = process.env.EMAIL_USER || 'your-email@example.com';
const EMAIL_PASS = process.env.EMAIL_PASS || 'your-email-password';
const RECIPIENT_EMAIL = process.env.RECIPIENT_EMAIL || 'recipient@example.com';
const LOG_FILE_PATH = process.env.LOG_FILE_PATH || path.join(process.cwd(), 'app.log');

const transporter = nodemailer.createTransport({
  service: EMAIL_SERVICE,
  auth: {
    user: EMAIL_USER,
    pass: EMAIL_PASS,
  },
});

async function emailLogFileAndRotate() {
  try {
    // 1. Read app.log content
    const logContent = await fs.promises.readFile(LOG_FILE_PATH, 'utf8');

    // 2. Email app.log content
    if (logContent.trim().length > 0) { // Only send email if there's content
      const mailOptions = {
        from: EMAIL_USER,
        to: RECIPIENT_EMAIL,
        subject: `[Log Rotation] app.log from ${new Date().toISOString().substring(0, 10)}`,
        text: 'Attached is the rotated log file.',
        attachments: [
          {
            filename: `app.log`,
            content: logContent,
          },
        ],
      };

      await transporter.sendMail(mailOptions);
      console.log('Log file emailed successfully.');
    } else {
      console.log('app.log is empty, skipping email.');
    }

    // 3. Rotate the log file
    const date = new Date();
    const rotatedFileName = `app-${date.getFullYear()}-${(date.getMonth() + 1).toString().padStart(2, '0')}-${date.getDate().toString().padStart(2, '0')}.log`;
    const rotatedFilePath = path.join(path.dirname(LOG_FILE_PATH), rotatedFileName);

    // Append content to the rotated file, creating it if it doesn't exist
    if (logContent.trim().length > 0) {
      await fs.promises.appendFile(rotatedFilePath, logContent + '\n', 'utf8');
      // Clear the original log file only after successful append
      await fs.promises.writeFile(LOG_FILE_PATH, '', 'utf8');
      console.log(`Log content appended to ${rotatedFileName} and app.log cleared.`);
    } else {
      console.log('app.log is empty, no content to append to rotated file.');
    }

  } catch (error) {
    console.error('Error during log rotation:', error);
  }
}

emailLogFileAndRotate();
