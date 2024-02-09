﻿
using System.IO;
using System;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.ViewModelCollection.Conversation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using System.Threading.Tasks;
using OpenAI_API;
using OpenAI_API.Models;
using TaleWorlds.Localization;
using System.Diagnostics;

namespace Bannerlord.ChatGPT
{
    public class NPCchatMissionChatVM : MissionConversationVM
    {
        private readonly ConversationManager _conversationManager;
        private string _newDialogText;
        private string _aIText;
        public string response;
        public OpenAIAPI bot;
        public OpenAI_API.Chat.Conversation _chat;
        private bool _isChating;
        public bool _isBotStarted;
        public bool isResponding;
        private String _APIkey;
        LoggingSystem _logSys;
        private string _currentResponse;
        private int _currentResponsePage;
        private int _fontsizeAIresponse;
        private int _chatBoxLength = 250;
        private int _totalPage;
        private PromotsEngine promotsEngine;

        public NPCchatMissionChatVM(Func<string> getContinueInputText, bool isLinksDisabled = false) : base(getContinueInputText, isLinksDisabled)
        {
            this._conversationManager = Campaign.Current.ConversationManager;
            _logSys = new LoggingSystem();
            FontsizeAIresponse = 27;
            AIText = "Enter your sentences and press ENTER to chat.";
            _isBotStarted = false;
            isResponding = false;
        }

        public async void StartBotAsync()
        {
            // read API file
            {
                try
                {
                    string modRelayerFolder = _logSys.modRelayerFolder;
                    // string FileName = modRelayerFolder + "APIkey.txt";
                    string FileName = Path.Combine(modRelayerFolder, "APIkey.txt");

                    //Pass the file path and file name to the StreamReader constructor
                    StreamReader sr = new StreamReader(FileName);
                    //Read the line of text
                    _APIkey = sr.ReadLine();
                    // close file
                    sr.Close();
                }
                catch (Exception e)
                {

                    _logSys.Addlog("API key is not properly read! Exception: " + e.Message);
                    InformationManager.DisplayMessage(new InformationMessage(
                               new TextObject("{=xO4QZbQ7XE}API key is not properly read! Please Check your /Modules/Bannerlord_ChatGPT/APIkey.txt").ToString()));
                    _isBotStarted = false;
                    return;
                }
                // remove all the space
                _APIkey = _APIkey.Replace(" ", "");
                _APIkey = _APIkey.Replace("\"", "");
                _APIkey = _APIkey.Replace("\n", "");
            }


            // Test chat 
            {
                try
                {
                    promotsEngine = new PromotsEngine(_conversationManager);
                    bot = new OpenAIAPI(_APIkey); // shorthand


                    _chat = bot.Chat.CreateConversation();
                    _chat.Model = Model.ChatGPTTurbo;
                    _chat.AppendSystemMessage(promotsEngine.GetPrompts());
                    _chat.AppendUserInput(new TextObject("{=t4szG41Y1s}Hi! I want to talk with you. (ChatGPT)").ToString());
                    AIText = await _chat.GetResponseFromChatbotAsync();
                }
                catch (Exception e)
                {
                    _isBotStarted = false;
                    _logSys.Addlog("ChatGPT didn't pass the test! Exception: " + e.Message);
                    InformationManager.DisplayMessage(new InformationMessage(
                               new TextObject("{=c40K7kO5Yr}ChatGPT didn't start successfully!").ToString()));
                    return;
                }
            }
            _isBotStarted = true;
            return;
        }

        public void ExitChating()
        {
            if (this._conversationManager != null)
            {
                _conversationManager.EndConversation();

            }
        }

        public async Task SendPlayerInputAsync()
        {
            if (this.UserText == String.Empty || UserText == null || IsChating == false || isResponding == true)
            {

                return;
            }
            FontsizeAIresponse = 27;
            isResponding = true;

            try
            {
                string userInput = UserText;
                _chat.AppendUserInput(UserText);
                UserText = new TextObject("{=s9eLLK10jE}Waiting for response").ToString();
                _currentResponse = await _chat.GetResponseFromChatbotAsync();
                _currentResponse = _currentResponse.Replace("\n", "");
                if (_currentResponse == null) { return; }

                _currentResponsePage = 1;
                _totalPage = (int)(Math.Ceiling(((double)_currentResponse.Length / (double)_chatBoxLength)));

                var characterName = Campaign.Current.ConversationManager.OneToOneConversationCharacter.Name.ToString();
                var conversationText = userInput + "\n" + _currentResponse;
                var lastMeetingTime = Hero.OneToOneConversationHero.LastMeetingTimeWithPlayer.ToString();

                promotsEngine.SaveConversationHistory(characterName, conversationText, lastMeetingTime);

                if (_currentResponse.Length > _chatBoxLength && _currentResponse != null)
                {
                    // FontsizeAIresponse = (int)((double)(27 * 250 / AIText.Length));

                    AIText = _currentResponse.Substring((_currentResponsePage - 1) * _chatBoxLength, _chatBoxLength * _currentResponsePage);
                    InformationManager.DisplayMessage(new InformationMessage(
                               new TextObject("{=0YlmsVWdKl}Left click to move to next page. Right click to move to previous page").ToString()));
                }
                else
                {
                    AIText = _currentResponse;
                }


            }
            catch (Exception e)
            {

                _logSys.Addlog("Conversation failed! Exception: " + e.Message);
            }
            isResponding = false;
            UserText = "";
            // SendJsonAsync(message);
        }


        public override void OnFinalize()
        {
            base.OnFinalize();
            CampaignEvents.PersuasionProgressCommittedEvent.ClearListeners((object)this);

        }

        internal void ExecuteContinue()
        {
            Debug.Print("ExecuteContinue", 0, Debug.DebugColor.White, 17592186044416UL);
            this._conversationManager.ContinueConversation();
        }

        internal void PreviousPage()
        {
            if (_currentResponsePage < 2)
            {
                return;
            }
            else
            {
                _currentResponsePage -= 1;
                int startIndex = (_currentResponsePage - 1) * _chatBoxLength;
                if (startIndex > 0)
                {
                    startIndex = FindWordBoundary(_currentResponse, startIndex, false);
                }
                int endIndex = Math.Min(startIndex + _chatBoxLength, _currentResponse.Length);
                AIText = _currentResponse.Substring(startIndex, endIndex - startIndex);
            }
        }

        internal void NextPage()
        {
            if (_currentResponsePage + 1 > _totalPage)
            {
                return;
            }
            else
            {
                _currentResponsePage += 1;
                int startIndex = (_currentResponsePage - 1) * _chatBoxLength;
                if (startIndex < _currentResponse.Length)
                {
                    startIndex = FindWordBoundary(_currentResponse, startIndex, true);
                }
                int endIndex;
                if (_currentResponsePage + 1 > _totalPage)
                {
                    endIndex = _currentResponse.Length;
                }
                else
                {
                    endIndex = Math.Min(startIndex + _chatBoxLength, _currentResponse.Length);
                    endIndex = FindWordBoundary(_currentResponse, endIndex, false);
                }
                AIText = _currentResponse.Substring(startIndex, endIndex - startIndex);
            }
        }

        private int FindWordBoundary(string text, int index, bool forward)
        {
            if (forward)
            {
                while (index < text.Length && !char.IsWhiteSpace(text[index])) index++;
            }
            else
            {
                while (index > 0 && !char.IsWhiteSpace(text[index - 1])) index--;
            }
            return index;
        }

        // The following is the sentence sent to chatGPT API
        // NewDialogText is the sentence written by user
        // ConversationText is the AI generated response

        [DataSourceProperty]
        public string UserText
        {
            get
            {
                return _newDialogText;
            }
            set
            {
                if (_newDialogText != value)
                {
                    _newDialogText = value;
                    OnPropertyChangedWithValue(value, "UserText");
                }
            }
        }


        [DataSourceProperty]
        public string AIText
        {
            get
            {
                return _aIText;
            }
            set
            {
                if (_aIText != value && value != null)
                {

                    _aIText = value;

                    OnPropertyChangedWithValue(value, "AIText");
                }
            }
        }

        [DataSourceProperty]
        public bool IsChating
        {
            get => this._isChating;
            set
            {
                if (this._isChating == value)
                    return;
                this._isChating = value;
                this.OnPropertyChangedWithValue((object)value, nameof(IsChating));
            }
        }

        [DataSourceProperty]
        public int FontsizeAIresponse
        {
            get => this._fontsizeAIresponse;
            set
            {
                if (_fontsizeAIresponse != value)
                {
                    _fontsizeAIresponse = value;
                    OnPropertyChangedWithValue(value, "FontsizeAIresponse");
                }
            }
        }
    }

}