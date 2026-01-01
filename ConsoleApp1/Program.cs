using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Xml.Linq;

class Program
{
    static readonly string JsonRelative = Path.Combine("data", "all_pokemon.json");

    static async Task Main()
    {
        List<PokemonClean> pokemonList;

        // 支援從目前工作目錄或執行目錄（bin）尋找 data/all_pokemon.json
        string jsonPath;
        string candidate1 = Path.Combine(Environment.CurrentDirectory, JsonRelative);
        string candidate2 = Path.Combine(AppContext.BaseDirectory, JsonRelative);

        if (File.Exists(candidate1))
        {
            jsonPath = candidate1;
        }
        else if (File.Exists(candidate2))
        {
            jsonPath = candidate2;
        }
        else
        {
            jsonPath = candidate1; // 預設儲存在目前工作目錄下的 relative path
        }

        if (File.Exists(jsonPath))
        {
            Console.WriteLine($" 偵測到現有的 {jsonPath}，將直接讀取本地資料！");
            Console.WriteLine("Environment.CurrentDirectory = " + Environment.CurrentDirectory);
            Console.WriteLine("AppContext.BaseDirectory = " + AppContext.BaseDirectory);
            pokemonList = LoadPokemonFromJson(jsonPath);
            Console.WriteLine($" 完成！成功讀取 {pokemonList.Count} 隻寶可夢");

            // 顯示前 5 隻寶可夢的資訊
            for (int i = 0; i < Math.Min(5, pokemonList.Count); i++)
            {
                var p = pokemonList[i];
                Console.WriteLine($"{p.id} - {p.name} ({p.type1}) ({(string.IsNullOrEmpty(p.type2) ? "null" : p.type2)}) 血量:{p.hp} 攻擊:{p.attack} 防禦:{p.defense} 特攻:{p.sp_atk} 特防:{p.sp_def} 速度:{p.speed}");
            }
            // 同步儲存到 SQLite
            try
            {
                PokemonDb.EnsureCreated();
                PokemonDb.SaveList(pokemonList);
                Console.WriteLine($" 已儲存 {pokemonList.Count} 筆到資料庫：{PokemonDb.DbPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("儲存到資料庫失敗：" + ex.Message);
            }

            return;
        }
        else
        {
            // --- Step 2: 沒有 JSON → 自動從 API 下載 ---
            Console.WriteLine(" 未找到 JSON，正在從 PokeAPI 下載資料...");
            string listUrl = "https://pokeapi.co/api/v2/pokemon?limit=100000&offset=0";
            async Task<JObject?> GetPokemonSpeciesAsync(int id)
            {
                using HttpClient client = new HttpClient();
                string url = $"https://pokeapi.co/api/v2/pokemon-species/{id}";

                try
                {
                    string json = await client.GetStringAsync(url);
                    JObject data = JObject.Parse(json); // 解析成 JObject
                    return data;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"name:{id}抓取失敗：{ex.Message}");
                    return null;
                }
            }
            async Task<string> GetChineseName(int id)
            {
                JObject? species = await GetPokemonSpeciesAsync(id);
                if (species == null) return "無";

                // names 陣列裡找 zh-Hant
                var zh = species["names"]?
                    .FirstOrDefault(n => n?["language"]?["name"]?.ToString() == "zh-Hant");

                return zh?["name"]?.ToString() ?? "無";
            }

            using (HttpClient client = new HttpClient())
            {
                // 1️ 取得全 Pokémon 清單
                string listJson = await client.GetStringAsync(listUrl);
                var listData = JsonConvert.DeserializeObject<PokemonListResponse>(listJson);

                if (listData == null || listData.results == null)
                {
                    Console.WriteLine("無法取得寶可夢列表。");
                    return;
                }

                List<Task<PokemonRaw>> tasks = new List<Task<PokemonRaw>>();

                // 2️ 平行下載每隻 Pokémon（過濾掉可能為 null 的 url）
                foreach (var item in listData.results)
                {
                    if (item == null) continue;
                    if (string.IsNullOrEmpty(item.url)) continue;
                    tasks.Add(DownloadPokemon(client, item.url));
                }

                PokemonRaw[] rawData = await Task.WhenAll(tasks);

                Console.WriteLine("下載中");

                // 3️ 整理成乾淨格式
                List<PokemonClean> cleanList = new List<PokemonClean>();

                foreach (var p in rawData)
                {
                    string chineseName = await GetChineseName(p.id);

                    // 如果中文不存在就用英文
                    string finalName = string.IsNullOrEmpty(chineseName) || chineseName == "無"
                                       ? (p.name ?? "無")
                                       : chineseName;
                    cleanList.Add(new PokemonClean
                    {
                        id = p.id,
                        name = finalName,
                        type1 = p.types?.Count > 0 ? p.types[0].type?.name : null,
                        type2 = p.types?.Count > 1 ? p.types[1].type?.name : null,

                        hp = p.stats?.Count > 0 ? p.stats[0].base_stat : 0,
                        attack = p.stats?.Count > 1 ? p.stats[1].base_stat : 0,
                        defense = p.stats?.Count > 2 ? p.stats[2].base_stat : 0,
                        sp_atk = p.stats?.Count > 3 ? p.stats[3].base_stat : 0,
                        sp_def = p.stats?.Count > 4 ? p.stats[4].base_stat : 0,
                        speed = p.stats?.Count > 5 ? p.stats[5].base_stat : 0,

                        height = p.height,
                        weight = p.weight,

                        abilities = string.Join(";", p.abilities?.ConvertAll(a => a?.ability?.name ?? "") ?? new List<string>()),
                        moves = string.Join(";", p.moves?.ConvertAll(m => m?.move?.name ?? "") ?? new List<string>()),
                    });
                }

                // 4️ 輸出 JSON
                SaveToJson(cleanList, jsonPath);
                Console.WriteLine($" 已輸出成 {jsonPath}");

                // 同步儲存到 SQLite 資料庫
                try
                {
                    PokemonDb.EnsureCreated();
                    PokemonDb.SaveList(cleanList);
                    Console.WriteLine($" 已儲存 {cleanList.Count} 筆到資料庫：{PokemonDb.DbPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("儲存到資料庫失敗：" + ex.Message);
                }
            }
        }

        // 如果程式從 API 下載了數據，會自動儲存並顯示
    }

    /// <summary>
    /// 將寶可夢列表儲存為 JSON 檔案
    /// </summary>
    static void SaveToJson(List<PokemonClean> list, string fileName)
    {
        string jsonString = JsonConvert.SerializeObject(list, Formatting.Indented);
        // 確保目錄存在（避免 DirectoryNotFoundException）
        var dir = Path.GetDirectoryName(fileName);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(fileName, jsonString);
    }

    /// <summary>
    /// 從 JSON 檔案讀取寶可夢列表
    /// </summary>
    static List<PokemonClean> LoadPokemonFromJson(string fileName)
    {
        string jsonString = File.ReadAllText(fileName);
        return JsonConvert.DeserializeObject<List<PokemonClean>>(jsonString) ?? new List<PokemonClean>();
    }
    static async Task<PokemonRaw> DownloadPokemon(HttpClient client, string url)
    {
        string json = await client.GetStringAsync(url);
        return JsonConvert.DeserializeObject<PokemonRaw>(json) ?? throw new InvalidOperationException("Failed to deserialize Pokemon data");
    }
}
