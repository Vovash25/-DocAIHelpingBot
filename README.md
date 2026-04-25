# -DocAIHelpingBot


DocAIHelpingBot is a smart Telegram bot built with C#. It uses powerful AI models to help users work with text and images.

## Core functionality

* Text summarization: The bot is integrated with the Groq API (`llama-3.1-8b-instant` model) to quickly extract the main idea from large volumes of text (2-5 sentences).
* Text recognition from photos (OCR): Uses Google Gemini (`gemini-2.5-flash`) to accurately extract readable text from uploaded images.
* Subscription system: The bot requires a subscription to a specific Telegram channel to access the functionality.
* User verification: Built-in local SQLite database (`bot_data.db`) for tracking and verifying users.

## Technology stack

* **Language:** C# (.NET)
* **Telegram API:** [Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot)
* **AI Integrations:** OpenAI SDK (used to access both Groq and Gemini APIs).
* **Database:** SQLite
