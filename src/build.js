import esbuild from 'esbuild';
import fs from 'node:fs/promises';
import path from 'node:path';

const distDir = './dist';

async function main() {
  await fs.rm(distDir, { recursive: true, force: true });
  await fs.mkdir(distDir, { recursive: true });

  // Build server.js
  await esbuild.build({
    entryPoints: ['./src/server.js'],
    bundle: true,
    minify: true,
    sourcemap: false,
    outfile: path.join(distDir, 'rss-over-email-arm64'),
    platform: 'node',
    target: 'node22',
    format: 'esm',
  });

  // Build log_rotator.js
  await esbuild.build({
    entryPoints: ['./src/log_rotator.js'],
    bundle: true,
    minify: true,
    sourcemap: false,
    outfile: path.join(distDir, 'log-rotator-arm64'),
    platform: 'node',
    target: 'node22',
    format: 'esm',
  });

  // Copy start.sh
  await fs.copyFile('./src/start.sh', path.join(distDir, 'start.sh'));

  // Copy rotate_logs.sh
  await fs.copyFile('./src/rotate_logs.sh', path.join(distDir, 'rotate_logs.sh'));

  console.log('Build completed successfully!');
}

main().catch((err) => {
  console.error('Build failed:', err);
  process.exit(1);
});
