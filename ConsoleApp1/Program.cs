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
    static readonly string JsonFile = "all_pokemon.json";

    static async Task Main()
    {
        // --- Step 1: 如果已有 JSON，就直接使用 ---
        if (File.Exists(JsonFile))
        {
            Console.WriteLine($" 偵測到現有的 {JsonFile}，將直接讀取本地資料！");
            Console.WriteLine(" 完成！");
        }
        else
        {
            // --- Step 2: 沒有 JSON → 自動從 API 下載 ---
            Console.WriteLine(" 未找到 JSON，正在從 PokeAPI 下載資料...");
            string listUrl = "https://pokeapi.co/api/v2/pokemon?limit=100000&offset=0";
            async Task<JObject> GetPokemonSpeciesAsync(int id)
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
                JObject species = await GetPokemonSpeciesAsync(id);
                if (species == null) return "無";

                // names 陣列裡找 zh-Hant
                var zh = species["names"]?
                    .FirstOrDefault(n => n["language"]["name"].ToString() == "zh-Hant");

                return zh != null ? zh["name"].ToString() : "無";
            }

            using (HttpClient client = new HttpClient())
            {
                // 1️ 取得全 Pokémon 清單
                string listJson = await client.GetStringAsync(listUrl);
                var listData = JsonConvert.DeserializeObject<PokemonListResponse>(listJson);

                List<Task<PokemonRaw>> tasks = new List<Task<PokemonRaw>>();

                // 2️ 平行下載每隻 Pokémon
                foreach (var item in listData.results)
                    tasks.Add(DownloadPokemon(client, item.url));

                PokemonRaw[] rawData = await Task.WhenAll(tasks);

                Console.WriteLine("下載中");

                // 3️ 整理成乾淨格式
                List<PokemonClean> cleanList = new List<PokemonClean>();

                foreach (var p in rawData)
                {
                    string chineseName = await GetChineseName(p.id);

                    // 如果中文不存在就用英文
                    string finalName = string.IsNullOrEmpty(chineseName) || chineseName == "無"
                                       ? p.name
                                       : chineseName;
                    cleanList.Add(new PokemonClean
                    {
                        id = p.id,
                        name = (await GetChineseName(p.id)),
                        type1 = p.types.Count > 0 ? p.types[0].type.name : null,
                        type2 = p.types.Count > 1 ? p.types[1].type.name : null,

                        hp = p.stats[0].base_stat,
                        attack = p.stats[1].base_stat,
                        defense = p.stats[2].base_stat,
                        sp_atk = p.stats[3].base_stat,
                        sp_def = p.stats[4].base_stat,
                        speed = p.stats[5].base_stat,

                        height = p.height,
                        weight = p.weight,

                        abilities = string.Join(";", p.abilities.ConvertAll(a => a.ability.name)),
                        moves = string.Join(";", p.moves.ConvertAll(m => m.move.name)),
                    });
                }

                // 4️ 輸出 JSON
                SaveToJson(cleanList, JsonFile);
                Console.WriteLine($" 已輸出成 {JsonFile}");
            }
        }

        var pokemonList = LoadPokemonFromJson(JsonFile);
        Console.WriteLine($"共讀取 {pokemonList.Count} 隻寶可夢");

        // 範例：列出前 5 隻
        for (int i = 0; i < 5; i++)
        {
            var p = pokemonList[i];
            Console.WriteLine($"{p.id} - {p.name} ({p.type1}) ({(string.IsNullOrEmpty(p.type2) ? "null" : p.type2)}) 血量:{p.hp} 攻擊:{p.attack} 防禦:{p.defense} 特攻:{p.sp_atk} 特防:{p.sp_def} 速度:{p.speed}");
        }
    }

    /// <summary>
    /// 將寶可夢列表儲存為 JSON 檔案
    /// </summary>
    static void SaveToJson(List<PokemonClean> list, string fileName)
    {
        string jsonString = JsonConvert.SerializeObject(list, Formatting.Indented);
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
