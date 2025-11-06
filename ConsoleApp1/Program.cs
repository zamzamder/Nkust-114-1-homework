using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

class Program
{
    static readonly string CsvFile = "all_pokemon.csv";

    static async Task Main()
    {
        // --- Step 1: 如果已有 CSV，就直接使用 ---
        if (File.Exists(CsvFile))
        {
            Console.WriteLine($" 偵測到現有的 {CsvFile}，將直接讀取本地資料！");
            Console.WriteLine(" 完成！");
        }
        else
        {
            // --- Step 2: 沒有 CSV → 自動從 API 下載 ---
            Console.WriteLine(" 未找到 CSV，正在從 PokeAPI 下載資料...");
            string listUrl = "https://pokeapi.co/api/v2/pokemon?limit=2000";
            async Task<JObject> GetPokemonSpeciesAsync(int id)
            {
                using HttpClient client = new HttpClient();
                string url = $"https://pokeapi.co/api/v2/pokemon-species/{id}/";

                try
                {
                    string json = await client.GetStringAsync(url);
                    JObject data = JObject.Parse(json); // 解析成 JObject
                    return data;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"抓取失敗：{ex.Message}");
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

                Console.WriteLine($" 下載完成，共 {rawData.Length} 隻");

                // 3️ 整理成乾淨格式
                List<PokemonClean> cleanList = new List<PokemonClean>();

                foreach (var p in rawData)
                {
                    cleanList.Add(new PokemonClean
                    {
                        id = p.id,
                        name =await GetChineseName(p.id),
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

                // 4️ 輸出 CSV
                SaveToCsv(cleanList, "all_pokemon.csv");
                Console.WriteLine(" 已輸出成 all_pokemon.csv");
            }
        }
        List<PokemonClean> LoadPokemonCleanCsv(string file)
        {
            List<PokemonClean> list = new List<PokemonClean>();
            string[] lines = File.ReadAllLines(file);

            for (int i = 1; i < lines.Length; i++) // 跳過標題列
            {
                var cols = CsvHelper.ParseCsvLine(lines[i]);

                PokemonClean p = new PokemonClean
                {
                    id = int.Parse(cols[0]),
                    name = cols[1],
                    type1 = cols[2],
                    type2 = cols[3],
                    hp = int.Parse(cols[4]),
                    attack = int.Parse(cols[5]),
                    defense = int.Parse(cols[6]),
                    sp_atk = int.Parse(cols[7]),
                    sp_def = int.Parse(cols[8]),
                    speed = int.Parse(cols[9]),
                    height = int.Parse(cols[10]),
                    weight = int.Parse(cols[11]),
                    abilities = cols[12],
                    moves = cols[13],

                };

                list.Add(p);
            }

            return list;
        }
        var pokemonList = LoadPokemonCleanCsv("all_pokemon.csv");
        Console.WriteLine($"共讀取 {pokemonList.Count} 隻寶可夢");

        // 範例：列出前 5 隻
        for (int i = 0; i < 5; i++)
        {
            var p = pokemonList[i];
            Console.WriteLine($"{p.id} - {p.name} ({p.type1}) ({(string.IsNullOrEmpty(p.type2) ? "null" : p.type2)}) 血量:{p.hp} 攻擊:{p.attack} 防禦:{p.defense} 特攻:{p.sp_atk} 特防:{p.sp_def} 速度:{p.speed}");
        }
    }

    /// <summary>
    /// CSV 安全字串（避免逗號破壞格式）
    /// </summary>
    static void SaveToCsv(List<PokemonClean> list, string fileName)
    {
        using (StreamWriter sw = new StreamWriter(fileName))
        {
            sw.WriteLine("id,name,type1,type2,hp,attack,defense,sp_atk,sp_def,speed,height,weight,abilities,moves");

            foreach (var p in list)
            {
                sw.WriteLine(
                    $"{p.id},{p.name},{p.type1},{p.type2},{p.hp},{p.attack},{p.defense},{p.sp_atk},{p.sp_def},{p.speed},{p.height},{p.weight},\"{p.abilities}\",\"{p.moves}\""
                );
            }
        }
    }
    static async Task<PokemonRaw> DownloadPokemon(HttpClient client, string url)
    {
        string json = await client.GetStringAsync(url);
        return JsonConvert.DeserializeObject<PokemonRaw>(json);
    }
}
