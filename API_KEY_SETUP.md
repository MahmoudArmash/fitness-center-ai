# Google Gemini API Key Setup Guide

## Quick Setup Options

### Option 1: Set in launchSettings.json (Easiest for Development)

1. Open `Properties/launchSettings.json`
2. Find the `GOOGLE_GEMINI_API_KEY` entry (it's already added)
3. Replace the empty string `""` with your actual API key:
   ```json
   "GOOGLE_GEMINI_API_KEY": "your-actual-api-key-here"
   ```
4. Restart your application

**Note:** This method is convenient for development but the file should NOT be committed to version control with your real API key.

### Option 2: Set Environment Variable (Recommended)

#### Windows PowerShell (Current Session Only)
```powershell
$env:GOOGLE_GEMINI_API_KEY="your-api-key-here"
dotnet run
```

#### Windows Command Prompt (Current Session Only)
```cmd
set GOOGLE_GEMINI_API_KEY=your-api-key-here
dotnet run
```

#### Windows (Permanent - System-Wide)
1. Press `Win + X` and select "System"
2. Click "Advanced system settings"
3. Click "Environment Variables"
4. Under "User variables" or "System variables", click "New"
5. Variable name: `GOOGLE_GEMINI_API_KEY`
6. Variable value: `your-api-key-here`
7. Click OK on all dialogs
8. **Restart your IDE/terminal** for changes to take effect

#### Windows (Permanent - User Only via PowerShell)
```powershell
[System.Environment]::SetEnvironmentVariable('GOOGLE_GEMINI_API_KEY', 'your-api-key-here', 'User')
```
Then restart your IDE/terminal.

#### Linux/Mac (Current Session)
```bash
export GOOGLE_GEMINI_API_KEY="your-api-key-here"
dotnet run
```

#### Linux/Mac (Permanent)
Add to your `~/.bashrc` or `~/.zshrc`:
```bash
echo 'export GOOGLE_GEMINI_API_KEY="your-api-key-here"' >> ~/.bashrc
source ~/.bashrc
```

### Option 3: Set in appsettings.json (Not Recommended for Production)

1. Open `appsettings.json` or `appsettings.Development.json`
2. Replace the empty string in `GoogleGemini:ApiKey`:
   ```json
   "GoogleGemini": {
     "ApiKey": "your-api-key-here",
     "Model": "gemini-2.5-flash"
   }
   ```
3. **Important:** Never commit this file with your real API key to version control!

## Get Your API Key

1. Go to [Google AI Studio](https://makersuite.google.com/app/apikey)
2. Sign in with your Google account
3. Click "Create API Key"
4. Copy the generated API key
5. Use it in one of the methods above

## Verify It's Working

After setting the API key, run your application and check the logs. You should see:
```
Configuration - Model: gemini-2.5-flash, API Key Present: True, Source: Environment Variable (User)
```

If you see `API Key Present: False`, the environment variable is not set correctly.

## Troubleshooting

### Issue: "API Key Present: False" even after setting environment variable

**Solution:**
1. Make sure you restarted your IDE/terminal after setting the variable
2. For Visual Studio: Close and reopen Visual Studio completely
3. For VS Code: Close and reopen VS Code
4. Try setting it in `launchSettings.json` instead (Option 1)

### Issue: Environment variable works in terminal but not in IDE

**Solution:**
- Use `launchSettings.json` method (Option 1) - this works directly in IDEs
- Or restart your IDE completely after setting the system environment variable

### Issue: API key was leaked/revoked

**Solution:**
1. Go to [Google AI Studio](https://makersuite.google.com/app/apikey)
2. Delete the old API key
3. Create a new API key
4. Update your environment variable or launchSettings.json with the new key

## Security Best Practices

✅ **DO:**
- Use environment variables for API keys
- Use `launchSettings.json` for local development (but don't commit real keys)
- Add `launchSettings.json` to `.gitignore` if it contains real keys
- Use different API keys for development and production

❌ **DON'T:**
- Commit API keys to version control (Git)
- Share API keys publicly
- Use the same API key for multiple projects
- Hardcode API keys in source code

