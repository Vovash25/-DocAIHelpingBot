using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using OpenAI.Chat; 
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ClientModel; 
using OpenAI;

using TMessage = Telegram.Bot.Types.Message;

var db = new Database();
string channelUsername = "@myprzyklad"; 
var botClient = new TelegramBotClient("-");

string groqApiKey = "-"; 
string geminiApiKey = "-"; 

var groqOptions = new OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1") };
var textClient = new ChatClient("llama-3.1-8b-instant", new ApiKeyCredential(groqApiKey), groqOptions);

var geminiOptions = new OpenAIClientOptions { Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/") };
var visionClient = new ChatClient("gemini-2.5-flash", new ApiKeyCredential(geminiApiKey), geminiOptions);

using var cts = new CancellationTokenSource();
var userStates = new Dictionary<long, UserState>();

botClient.StartReceiving(HandleUpdate, HandleError, cancellationToken: cts.Token);
Console.WriteLine("🚀 Бот @DocAIHelpingBot work (Text: Groq | Photo: Gemini)");
Console.WriteLine("🔈 Чекаю повідомлень у Telegram...");
await Task.Delay(-1); 


async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken ct)
{
    if (update.Message is { Type: MessageType.Photo, Photo: { } photos } && userStates.GetValueOrDefault(update.Message.Chat.Id) == UserState.WaitingForOCR)
    {
        await HandlePhotoOCR(bot, update.Message, photos.Last().FileId, ct);
        return;
    }

    if (update.Message is not { Text: { } messageText } message || message.From is null) return;
    long chatId = message.Chat.Id;

    try {
        var chatMember = await bot.GetChatMemberAsync(channelUsername, message.From.Id, cancellationToken: ct);
        if (chatMember.Status == ChatMemberStatus.Left || chatMember.Status == ChatMemberStatus.Kicked) {
            await bot.SendTextMessageAsync(chatId, $"Привіт! Підпишіться на {channelUsername}, щоб активувати бота.", 
                replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("Підписатися", $"https://t.me/{channelUsername.Replace("@", "")}")), cancellationToken: ct);
            return;
        }
    } catch { Console.WriteLine("⚠️ Помилка перевірки підписки."); }

    if (messageText == "/start") {
        userStates[chatId] = UserState.None;
        await ShowMainMenu(bot, chatId, ct);
    }
    else if (messageText == "📝 Скоротити текст") {
        userStates[chatId] = UserState.WaitingForSummary;
        await bot.SendTextMessageAsync(chatId, "Надішліть текст для аналізу:", cancellationToken: ct);
    }
    else if (messageText == "🔍 Текст із фото") {
        userStates[chatId] = UserState.WaitingForOCR;
        await bot.SendTextMessageAsync(chatId, "Надішліть фото з текстом:", cancellationToken: ct);
    }
    else if (messageText == "✅ Верифікація") {
        db.VerifyUser(chatId);
        await bot.SendTextMessageAsync(chatId, "Верифіковано!", cancellationToken: ct);
    }
    else {
        await ProcessAiLogic(bot, chatId, messageText, ct);
    }
}

async Task ProcessAiLogic(ITelegramBotClient bot, long chatId, string text, CancellationToken ct)
{
    if (!userStates.ContainsKey(chatId) || userStates[chatId] == UserState.None) return;

    if (userStates[chatId] == UserState.WaitingForSummary) {
        await bot.SendTextMessageAsync(chatId, "⏳ Groq аналізує текст...", cancellationToken: ct);
        
        try 
        {
            var messages = new List<ChatMessage> 
            { 
                new SystemChatMessage("Ти професійний асистент. Прочитай текст і коротко виділи його головну думку (2-5 речення). Відповідай мовою, якою звертався до тебе користувач. Не додавай жодних коментарів, просто дай стисле резюме головної ідеї тексту."),
                new UserChatMessage(text) 
            };
            
            var completion = await textClient.CompleteChatAsync(messages, cancellationToken: ct);
            await bot.SendTextMessageAsync(chatId, $"✅ Головна думка:\n\n{completion.Value.Content[0].Text}", cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API Error (Text): {ex.Message}");
            await bot.SendTextMessageAsync(chatId, "❌ Виникла помилка при зверненні до ШІ. Спробуй ще раз.", cancellationToken: ct);
        }
        finally
        {
            userStates[chatId] = UserState.None;
        }
    }
}

async Task HandlePhotoOCR(ITelegramBotClient bot, TMessage message, string fileId, CancellationToken ct)
{
    await bot.SendTextMessageAsync(message.Chat.Id, "🧐 Бот зчитує текст із зображення...", cancellationToken: ct);
    
    try 
    {
        var file = await bot.GetFileAsync(fileId, cancellationToken: ct);
        using var ms = new MemoryStream();
        await bot.DownloadFileAsync(file.FilePath!, ms, cancellationToken: ct);
        
        var chatMessages = new List<ChatMessage>
        {
            new SystemChatMessage("Ти розумний OCR-сканер. Витягни весь читабельний текст із цієї фотографії і поверни його користувачу без жодних додаткових коментарів."),
            new UserChatMessage(
                ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(ms.ToArray()), "image/jpeg")
            )
        };

        var completion = await visionClient.CompleteChatAsync(chatMessages, cancellationToken: ct);
        await bot.SendTextMessageAsync(message.Chat.Id, $"📄 Розпізнаний текст:\n\n{completion.Value.Content[0].Text}", cancellationToken: ct);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"API Error (OCR): {ex.Message}");
        await bot.SendTextMessageAsync(message.Chat.Id, "❌ Не вдалося розпізнати фото. Перевір, чи фото чітке.", cancellationToken: ct);
    }
    finally
    {
        userStates[message.Chat.Id] = UserState.None;
    }
}

async Task ShowMainMenu(ITelegramBotClient bot, long chatId, CancellationToken ct)
{
    var menu = new ReplyKeyboardMarkup(new[] {
        new KeyboardButton[] { "📝 Скоротити текст", "🔍 Текст із фото" },
        new KeyboardButton[] { "✅ Верифікація" }
    }) { ResizeKeyboard = true };
    await bot.SendTextMessageAsync(chatId, "Головне меню DocAI:", replyMarkup: menu, cancellationToken: ct);
}

async Task HandleError(ITelegramBotClient bot, Exception ex, CancellationToken ct) { Console.WriteLine($"Error: {ex.Message}"); await Task.CompletedTask; }

enum UserState { None, WaitingForSummary, WaitingForImagePrompt, WaitingForOCR }