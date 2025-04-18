﻿#pragma warning disable CS0436 // Type conflicts with imported type
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Players;
using BazaarGameShared.Infra.Messages;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using TheBazaar;
using UnityEngine;
using System.Net.Http;
using System.Text;
using BepInEx.Configuration;
using System.IO;
using System.Threading;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace BazaarPlannerMod;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private readonly Harmony _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
    private static DateTime _lastSentTime = DateTime.MinValue;
    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(2);
    private static string FirebaseApiKey = "AIzaSyCrDTf9_S8PURED8DZBDbbEsJuMA1poduw";
    private static string _runId;
    private static ConfigEntry<string> UidConfig;
    private static ConfigEntry<string> TokenConfig;
    private static ConfigEntry<string> RefreshTokenConfig;
    private static ConfigEntry<string> TokenExpiryConfig;
    private static ConfigFile BPConfig;
    private static string _lastBoardState = "";
    private static int _encounterId = 0;
    private static ConfigEntry<string> DisplayNameConfig;
    private static DateTime _lastUpdateTime = DateTime.MinValue;
    private static CancellationTokenSource _updateCancellationToken;
    private static EVictoryCondition _lastVictoryCondition;
    private static string _firebaseUrl = "https://bazaarplanner-default-rtdb.firebaseio.com/";
    private static Dictionary<string, List<string>> _baseItemTags;

    private static async Task SaveCombat()
    {        
        string uid = UidConfig.Value;
        RunInfo runInfo = getRunInfo();
        string json = CreateBazaarPlannerJson(runInfo);
        string compressed = LZString.CompressToEncodedURIComponent(json);
        string runId = runInfo.RunId;
        string battleName = $"Day {Data.Run.Day} - {runInfo.OppName}";
        string timestamp = ((DateTimeOffset)DateTime.Now).ToUnixTimeMilliseconds().ToString();
        var data = new Dictionary<string, object>
        {
            { "wins", runInfo.Wins },
            { "losses", runInfo.Losses },
            { "day", runInfo.Day },
            { "t", timestamp },
            { "hero", runInfo.Hero },
            { "lastEncounter", _encounterId.ToString() },
            { $"encounters/{_encounterId}", new
            {
                name = battleName,
                d = compressed,
                t = timestamp,
                v = _lastVictoryCondition==EVictoryCondition.Win ? 1 : 0
            }}
        };

        await SaveToFirebase($"users/{uid}/runs/{runId}", data);
        _encounterId++;
       // await SaveToFirebase($"users/{uid}/currentRun/id",runId);       
    }

    private static async Task SaveToFirebase(string url, object data)
    {        
        try 
        {
            var token = await GetValidToken();
            
            if (string.IsNullOrEmpty(UidConfig.Value) || string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Cannot save to BazaarPlanner: Missing UID or token");
                return;
            }
            
            using (var httpClient = new HttpClient())
            {
                var jsonData = JsonConvert.SerializeObject(data);
                Console.WriteLine($"Attempting to save to {url} with data: {jsonData}");
                
                var request = new HttpRequestMessage
                {
                    Method = new HttpMethod("PATCH"),
                    RequestUri = new Uri($"{_firebaseUrl}{url}.json?auth={token}"),
                    Content = new StringContent(jsonData, Encoding.UTF8, "application/json")
                };
                
                var response = await httpClient.SendAsync(request);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"{url} saved successfully");
                }
                else
                {
                    Console.WriteLine($"Failed to save {url}: {response.StatusCode} - {response.ReasonPhrase}");
                    Console.WriteLine($"Response content: {responseContent}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SaveToFirebase: {ex.Message}");
        }
    }
     private static RunInfo getRunInfo() {
        return new RunInfo
        {
            Wins = Data.Run.Victories,
            Losses = Data.Run.Losses,
            Hero = Data.Run.Player.Hero.ToString(),
            Day = (int)Data.Run.Day,
            Gold = Data.Run.Player.GetAttributeValue(EPlayerAttributeType.Gold),
            Income = Data.Run.Player.GetAttributeValue(EPlayerAttributeType.Income),
            Cards = GetCardInfo(GetItemsAsCards(Data.Run.Player.Hand)),
            Stash = GetCardInfo(GetItemsAsCards(Data.Run.Player.Stash)),
            Skills = GetSkillInfo(Data.Run.Player.Skills),
            OppCards = GetCardInfo(GetItemsAsCards(Data.Run.Opponent?.Hand)),
            OppStash = GetCardInfo(GetItemsAsCards(Data.Run.Opponent?.Stash)),
            OppSkills = GetSkillInfo(Data.Run.Opponent?.Skills),
            Health = Data.Run.Player.GetAttributeValue(EPlayerAttributeType.HealthMax),
            Regen = Data.Run.Player.GetAttributeValue(EPlayerAttributeType.HealthRegen),
            Level = Data.Run.Player.GetAttributeValue(EPlayerAttributeType.Level),
            Prestige = Data.Run.Player.GetAttributeValue(EPlayerAttributeType.Prestige),
            Name = Data.Profile?.Username,
            OppHealth = Data.Run.Opponent?.GetAttributeValue(EPlayerAttributeType.HealthMax),
            OppRegen = Data.Run.Opponent?.GetAttributeValue(EPlayerAttributeType.HealthRegen),
            OppName = Data.Run.Opponent?.Hero==EHero.Common ? "PvE":Data.SimPvpOpponent?.Name,
            OppHero = Data.Run.Opponent?.Hero.ToString(),
            OppGold = Data.Run.Opponent?.GetAttributeValue(EPlayerAttributeType.Gold),
            OppIncome = Data.Run.Opponent?.GetAttributeValue(EPlayerAttributeType.Income),
            OppLevel = Data.Run.Opponent?.GetAttributeValue(EPlayerAttributeType.Level),
            OppPrestige = Data.Run.Opponent?.GetAttributeValue(EPlayerAttributeType.Prestige),
            RunId = _runId
        };
    }
    private static List<Card> GetItemsAsCards(IPlayerInventory container)
    {
        return container.Container.GetSocketables()
            .Cast<Card>() // Cast all items to Card (throws InvalidCastException if any object is incompatible)
            .ToList<Card>();
    }

    static List<RunInfo.SkillInfo> GetSkillInfo(IEnumerable<SkillCard> skills)
    {
        List<RunInfo.SkillInfo> skillInfos = new List<RunInfo.SkillInfo>();
        foreach (var skill in skills)
        {
            if (skill.Template != null)
                skillInfos.Add(new RunInfo.SkillInfo
                {
                    TemplateId = skill.TemplateId,
                    Tier = skill.Tier,
                    Name = skill.Template.Localization.Title.Text
                });
        }

        return skillInfos;
    }

    private static string CreateBazaarPlannerJson(RunInfo runInfo)
    {
        var result = new List<object>();

        // Add player and opponent data objects (unchanged)
        result.Add(new
        {
            name = "_b_b",
            health = runInfo.Health,
            regen = runInfo.Regen,
            playerName = DisplayNameConfig.Value ?? runInfo.Name ?? "Unknown",
            hero = runInfo.Hero,
            level = runInfo.Level,
            prestige = runInfo.Prestige,
            income = runInfo.Income,
            gold = runInfo.Gold,
            skills = runInfo.Skills?.Select(s => new
            {
                name = s.Name,
                tier = s.Tier
            }).ToList(),
        });

        if (runInfo.OppCards != null && runInfo.OppCards.Count > 0) {
            result.Add(new
            {
                name = "_b_t",
                gold = runInfo.OppGold,
                health = runInfo.OppHealth,
                regen = runInfo.OppRegen,
                playerName = runInfo.OppName ?? "Unknown",
                hero = runInfo.OppHero,
                level = runInfo.OppLevel,
                income = runInfo.OppIncome,
                prestige = runInfo.OppPrestige,
                day = runInfo.Day,
                skills = runInfo.OppSkills?.Select(s => new
                {
                    name = s.Name,
                    tier = s.Tier
                }).ToList()
            });
        }
        result.Add(new 
        {
            name = "_b_backpack"
        });

        // Helper function to create card object with conditional attributes
        object CreateCardObject(RunInfo.CardInfo card, string board)
        {
            var cardDict = new Dictionary<string, object>
            {
                ["name"] = card.Enchant.Length > 0 ? card.Enchant + " " + card.Name : card.Name,
                ["startIndex"] = card.Left,
                ["board"] = board,
                ["tier"] = card.Tier
            };
           
            // Only include tags if they differ from base item
            if (card.Tags != null && card.Tags.Count > 0 && HasNewTags(card.Name, card.Tags.Select(t => t.ToString()).ToList()))
            {
                cardDict["tags"] = card.Tags.Select(t => t.ToString()).ToList();
            }
            
            if (card.Attributes?.ContainsKey(ECardAttributeType.SellPrice) == true)
                cardDict["valueFinal"] = card.Attributes[ECardAttributeType.SellPrice];
            
            if (card.Attributes?.ContainsKey(ECardAttributeType.HealAmount) == true)
                cardDict["healFinal"] = card.Attributes[ECardAttributeType.HealAmount];
            
            if (card.Attributes?.ContainsKey(ECardAttributeType.CooldownMax) == true)
                cardDict["cooldownFinal"] = card.Attributes[ECardAttributeType.CooldownMax];
            
            if (card.Attributes?.ContainsKey(ECardAttributeType.CritChance) == true && card.Attributes[ECardAttributeType.CritChance]>0)
                cardDict["critFinal"] = card.Attributes[ECardAttributeType.CritChance];
            
            if (card.Attributes?.ContainsKey(ECardAttributeType.BurnApplyAmount) == true)
                cardDict["burnFinal"] = card.Attributes[ECardAttributeType.BurnApplyAmount];
            if (card.Attributes?.ContainsKey(ECardAttributeType.ShieldApplyAmount) == true)
                cardDict["shieldFinal"] = card.Attributes[ECardAttributeType.ShieldApplyAmount];
            if (card.Attributes?.ContainsKey(ECardAttributeType.PoisonApplyAmount) == true)
                cardDict["poisonFinal"] = card.Attributes[ECardAttributeType.PoisonApplyAmount];
            if (card.Attributes?.ContainsKey(ECardAttributeType.DamageAmount) == true)
                cardDict["damageFinal"] = card.Attributes[ECardAttributeType.DamageAmount];
            if (card.Attributes?.ContainsKey(ECardAttributeType.Lifesteal) == true && card.Attributes[ECardAttributeType.Lifesteal]>0)
                cardDict["lifestealFinal"] = card.Attributes[ECardAttributeType.Lifesteal];
            if (card.Attributes?.ContainsKey(ECardAttributeType.RegenApplyAmount) == true)
                cardDict["regenFinal"] = card.Attributes[ECardAttributeType.RegenApplyAmount];
            if (card.Attributes?.ContainsKey(ECardAttributeType.AmmoMax) == true && card.Attributes[ECardAttributeType.AmmoMax]>0)
                cardDict["ammoFinal"] = card.Attributes[ECardAttributeType.AmmoMax];
            return cardDict;
        }

        // Add player cards
        if (runInfo.Cards != null)
        {
            foreach (var card in runInfo.Cards)
            {
                result.Add(CreateCardObject(card, "b"));
            }
        }

        // Add opponent cards
        if (runInfo.OppCards != null)
        {
            foreach (var card in runInfo.OppCards)
            {
                result.Add(CreateCardObject(card, "t"));
            }
        }
        if(runInfo.Stash != null)
        {
            foreach (var card in runInfo.Stash)
            {
                result.Add(CreateCardObject(card, "backpack"));
            }
        }

        return JsonConvert.SerializeObject(result);
    }

    static void OpenInBazaarPlanner(string compressedData)
    {
        try
        {
    
            string url = $"https://www.bazaarplanner.com/#{compressedData}";
            
            Application.OpenURL(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening BazaarPlanner: {ex.Message}");
        }
    }
    
    protected virtual void Awake()
    {
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        _harmony.PatchAll();
        
        // Load config
        BPConfig = new ConfigFile(Path.Combine(Paths.ConfigPath, "BazaarPlanner.cfg"), true);
        try
        {
            Console.WriteLine("Initializing configurations...");
            UidConfig = BPConfig.Bind("Authentication", "Uid", "", "Firebase User ID");
            TokenExpiryConfig = BPConfig.Bind("Authentication", "TokenExpiry", DateTime.MinValue.ToString(), "Token Expiration Time");
            TokenConfig = BPConfig.Bind("Authentication", "Token", "", "Firebase ID Token");
            RefreshTokenConfig = BPConfig.Bind("Authentication", "RefreshToken", "", "Firebase Refresh Token");
            DisplayNameConfig = BPConfig.Bind("Authentication", "DisplayName", "", "Display Name");
            
            Console.WriteLine("Configurations initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing configurations: {ex.Message}");
        }

        // Load base items data on startup
        LoadBaseItems();
    }

    private void LoadBaseItems()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("BazaarPlannerMod.items.js"))
            using (var reader = new StreamReader(stream))
            {
                string content = reader.ReadToEnd();
                
                // Remove the "export const items = " part and any trailing semicolon
                content = content.Replace("export const items =", "")
                                .Trim()
                                .TrimEnd(';');
                
                // Deserialize to dynamic to easily access the nested structure
                var items = JObject.Parse(content);
                
                // Create a simplified dictionary with just the tags
                _baseItemTags = items.Properties().ToDictionary(
                    prop => prop.Name,
                    prop => prop.Value["tags"].Select(t => t.ToString()).ToList()
                );
                
                Logger.LogInfo($"Loaded tags for {_baseItemTags.Count} base items");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load base items: {ex.Message}");
            _baseItemTags = new Dictionary<string, List<string>>();
        }
    }

    private static bool HasNewTags(string itemName, List<string> currentTags)
    {
        if (!_baseItemTags.TryGetValue(itemName, out var baseTags))
            return true; // If we don't have base data, include tags

        if (currentTags == null || currentTags.Count == 0)
            return false;

        // Check if any current tag is not in base tags
        return currentTags.Any(tag => !baseTags.Contains(tag));
    }

    static List<RunInfo.CardInfo> GetCardInfo(List<Card> cards)
    {
        List<RunInfo.CardInfo> cardInfos = new List<RunInfo.CardInfo>();
        foreach (var card in cards)
        {
                cardInfos.Add(new RunInfo.CardInfo
                {
                    TemplateId = card.TemplateId,
                    Tier = card.Tier,
                    Left = card.LeftSocketId,
                    Instance = card.GetInstanceId(),
                    Attributes = card.Attributes,
                    Tags = card.Tags,
                    Name = card.Template?.InternalName,
                    Enchant = card.GetEnchantment().ToString()                    
                });
        }
        return cardInfos;
    }

    [HarmonyPatch(typeof(BoardManager), "Update")]
    class Update
    {
        [HarmonyPrefix]
        static void Prefix()
        {
            if (!Input.GetKeyDown(KeyCode.B))
            {
                return;
            }

            if (DateTime.Now - _lastSentTime < SendInterval)
            {
                return;
            }

            _lastSentTime = DateTime.Now;

            RunInfo runInfo = getRunInfo();
            string json = CreateBazaarPlannerJson(runInfo);
            string compressed = LZString.CompressToEncodedURIComponent(json);
            Task.Run(() => OpenInBazaarPlanner(compressed));
        }
    }

    private static async Task<T> ReadFromFirebase<T>(string path, bool shallow = false)
    {
        try
        {
            var token = await GetValidToken();
            using (var client = new HttpClient())
            {
                var shallowParam = shallow ? "&shallow=true" : "";
                var response = await client.GetAsync($"{_firebaseUrl}{path}.json?auth={token}{shallowParam}");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(result) && result != "null")
                    {
                        return JsonConvert.DeserializeObject<T>(result);
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to fetch from Firebase: {response.ReasonPhrase}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading from Firebase: {ex.Message}");
        }
        return default(T);
    }

    [HarmonyPatch(typeof(AppState), "OnRunInitializedMessageReceived")]
    public static class OnRunInitializedMessageReceived
    {
        [HarmonyPrefix]
        static async void Prefix(NetMessageRunInitialized obj)
        {
            _runId = obj.RunId;
            var encounters = await ReadFromFirebase<Dictionary<string, object>>($"users/{UidConfig.Value}/runs/{obj.RunId}/encounters", shallow: true);
            _encounterId = encounters?.Count ?? 0;
        }
    }

    private static async Task<string> GetValidToken()
    {
        try 
        {           
            DateTime tokenExpiry;
            if (!DateTime.TryParse(TokenExpiryConfig.Value, out tokenExpiry))
            {
                tokenExpiry = DateTime.MinValue;
            }

            //Console.WriteLine($"Current token expires: {tokenExpiry}");
            if (DateTime.Now < tokenExpiry && !string.IsNullOrEmpty(TokenConfig.Value))
            {
              //  Console.WriteLine("Using existing valid token");
                return TokenConfig.Value;
            }

            if (string.IsNullOrEmpty(RefreshTokenConfig.Value))
            {
                Console.WriteLine("No refresh token found in config");
                return null;
            }

            //Console.WriteLine("All config values present, attempting to refresh token...");
            using (var client = new HttpClient())
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "refresh_token", RefreshTokenConfig.Value }
                });
              //  Console.WriteLine("Content: " + content + " and " + FirebaseApiKey);

                var response = await client.PostAsync(
                    $"https://securetoken.googleapis.com/v1/token?key={FirebaseApiKey}",
                    content
                );

//                Console.WriteLine($"Token refresh response status: {response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error response content: {errorContent}");
                }

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                        await response.Content.ReadAsStringAsync()
                    );

                    TokenConfig.Value = result["id_token"];
                    RefreshTokenConfig.Value = result["refresh_token"];
                    TokenExpiryConfig.Value = DateTime.Now.AddHours(1).ToString();

                    return TokenConfig.Value;
                } else {
                    Console.WriteLine("Failed to refresh token: " + response.ReasonPhrase);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetValidToken: {ex.GetType().Name} - {ex.Message}");
            return null;
        }
        return null;
    }    

    [HarmonyPatch(typeof(CombatSimHandler), "Simulate")]
    class CombatSimHandlerSimulate
    {
        [HarmonyPrefix]
        static void Prefix(NetMessageCombatSim message, CancellationTokenSource cancellationToken)
        {
            if(UidConfig.Value == null || UidConfig.Value == "") return;
            _lastVictoryCondition = message.Data.Winner == ECombatantId.Player ? EVictoryCondition.Win : EVictoryCondition.Lose;
            Task.Run(() => SaveCombat());
        }
    }

    [HarmonyPatch(typeof(BoardManager), "UpdateBoard")]
    public static class UpdateBoardPatch 
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if(UidConfig.Value == null || UidConfig.Value == "") return;
            RunInfo runInfo = getRunInfo();
            string json = CreateBazaarPlannerJson(runInfo);

            if (json != _lastBoardState)
            {
                _lastBoardState = json;
                string compressed = LZString.CompressToEncodedURIComponent(json);
                var saveData = new {
                    id = runInfo.RunId,
                    d = compressed
                };

                if (_updateCancellationToken == null)
                {
                    // No pending update, do it immediately
                    Task.Run(() => SaveToFirebase($"users/{UidConfig.Value}/currentrun", saveData));
                }
                else
                {
                    // Cancel pending update and schedule new one
                    _updateCancellationToken.Cancel();
                    _updateCancellationToken = new CancellationTokenSource();
                    
                    Task.Delay(1000, _updateCancellationToken.Token)
                        .ContinueWith(t => {
                            if (!t.IsCanceled) {
                                Task.Run(() => SaveToFirebase($"users/{UidConfig.Value}/currentrun", compressed));
                                _updateCancellationToken = null;
                            }
                        }, TaskContinuationOptions.OnlyOnRanToCompletion);
                }
            }
        }
    }
}