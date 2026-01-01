using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ConsoleApp1.Web.Controllers
{
    public class PokemonController : Controller
    {
        // 嘗試在多個可能位置尋找資料庫
        private string? FindDatabasePath()
        {
            string fileName = "pokemon.db";
            // 常見位置： workspace/data, project/../data, AppContext.BaseDirectory/../..../data
            var candidates = new List<string>();
            // workspace root (assume one level up from project folder)
            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "data", fileName));
            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "data", fileName));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "data", fileName));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "..", "..", "data", fileName));
            candidates.Add(Path.Combine(Environment.CurrentDirectory, "data", fileName));

            foreach (var c in candidates)
            {
                var full = Path.GetFullPath(c);
                if (System.IO.File.Exists(full)) return full;
            }

            // 最後在整個磁碟向上搜尋最多 5 層
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 6 && dir != null; i++)
            {
                var attempt = Path.Combine(dir.FullName, "data", fileName);
                if (System.IO.File.Exists(attempt)) return Path.GetFullPath(attempt);
                dir = dir.Parent;
            }

            return null;
        }

        public IActionResult Index(string? q, int page = 1, string? sortBy = null, string? sortDir = null)
        {
            var list = new List<(int Id, string Name, int? Hp, int? Attack, int? Defense, int? SpAtk, int? SpDef, int? Speed, int? Height, int? Weight, string? Abilities)>();
            var dbPath = FindDatabasePath();
            if (string.IsNullOrEmpty(dbPath))
            {
                ViewBag.Message = $"找不到資料庫 (pokemon.db)。請確認 data/pokemon.db 存在。";
                return View(list);
            }

            const int pageSize = 20;
            if (page < 1) page = 1;

            // 處理排序參數
            var allowed = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "id", "Id" },
                { "name", "Name" },
                { "hp", "Hp" },
                { "attack", "Attack" },
                { "defense", "Defense" },
                { "spatk", "SpAtk" },
                { "spdef", "SpDef" },
                { "speed", "Speed" },
                { "height", "Height" },
                { "weight", "Weight" },
                { "abilities", "Abilities" }
            };
            var orderCol = "Id";
            if (!string.IsNullOrWhiteSpace(sortBy) && allowed.ContainsKey(sortBy)) orderCol = allowed[sortBy!];
            var dir = "ASC";
            if (!string.IsNullOrWhiteSpace(sortDir) && sortDir.Equals("desc", System.StringComparison.OrdinalIgnoreCase)) dir = "DESC";
            ViewBag.SortBy = sortBy;
            ViewBag.SortDir = dir.Equals("ASC", System.StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";

            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();

            if (string.IsNullOrWhiteSpace(q))
            {
                ViewBag.Query = null;

                using (var cntCmd = conn.CreateCommand())
                {
                    cntCmd.CommandText = "SELECT COUNT(*) FROM Pokemon";
                    var total = Convert.ToInt32(cntCmd.ExecuteScalar());
                    var totalPages = (int)Math.Ceiling(total / (double)pageSize);
                    if (page > totalPages && totalPages > 0) page = totalPages;
                    ViewBag.Page = page;
                    ViewBag.TotalPages = totalPages;
                    // 計算顯示的頁碼視窗（最多顯示 20 頁）
                    int windowSize = 20;
                    int start = page - windowSize / 2;
                    if (start < 1) start = 1;
                    int end = start + windowSize - 1;
                    if (end > totalPages)
                    {
                        end = totalPages;
                        start = Math.Max(1, end - windowSize + 1);
                    }
                    ViewBag.PageStart = start;
                    ViewBag.PageEnd = end;
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT Id, Name, Hp, Attack, Defense, SpAtk, SpDef, Speed, Height, Weight, Abilities FROM Pokemon ORDER BY {orderCol} {dir} LIMIT @limit OFFSET @offset";
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var id = rdr.GetInt32(0);
                    var name = rdr.IsDBNull(1) ? "(null)" : rdr.GetString(1);
                    int? hp = rdr.IsDBNull(2) ? null : (int?)rdr.GetInt32(2);
                    int? attack = rdr.IsDBNull(3) ? null : (int?)rdr.GetInt32(3);
                    int? defense = rdr.IsDBNull(4) ? null : (int?)rdr.GetInt32(4);
                    int? spatk = rdr.IsDBNull(5) ? null : (int?)rdr.GetInt32(5);
                    int? spdef = rdr.IsDBNull(6) ? null : (int?)rdr.GetInt32(6);
                    int? speed = rdr.IsDBNull(7) ? null : (int?)rdr.GetInt32(7);
                    int? height = rdr.IsDBNull(8) ? null : (int?)rdr.GetInt32(8);
                    int? weight = rdr.IsDBNull(9) ? null : (int?)rdr.GetInt32(9);
                    string? abilities = rdr.IsDBNull(10) ? null : rdr.GetString(10);
                    list.Add((id, name, hp, attack, defense, spatk, spdef, speed, height, weight, abilities));
                }
            }
            else
            {
                ViewBag.Query = q;
                // 如果 q 是數字 → 檢查該 Id 是否存在，存在則導向 Details，否則在本頁顯示錯誤訊息
                if (int.TryParse(q.Trim(), out int id))
                {
                    using var existCmd = conn.CreateCommand();
                    existCmd.CommandText = "SELECT COUNT(*) FROM Pokemon WHERE Id = @id";
                    existCmd.Parameters.AddWithValue("@id", id);
                    var exists = Convert.ToInt32(existCmd.ExecuteScalar());
                    if (exists > 0)
                    {
                        return RedirectToAction(nameof(Details), new { id });
                    }
                    ViewBag.Message = $"找不到編號 {id} 的寶可夢。";
                    ViewBag.Query = q;
                    ViewBag.Page = 1;
                    ViewBag.TotalPages = 0;
                    ViewBag.PageStart = 1;
                    ViewBag.PageEnd = 1;
                    return View(list);
                }

                using (var cntCmd = conn.CreateCommand())
                {
                    cntCmd.CommandText = "SELECT COUNT(*) FROM Pokemon WHERE Name LIKE @name";
                    cntCmd.Parameters.AddWithValue("@name", "%" + q.Trim() + "%");
                    var total = Convert.ToInt32(cntCmd.ExecuteScalar());
                    var totalPages = (int)Math.Ceiling(total / (double)pageSize);
                    if (page > totalPages && totalPages > 0) page = totalPages;
                    ViewBag.Page = page;
                    ViewBag.TotalPages = totalPages;
                        // 計算顯示的頁碼視窗（最多顯示 20 頁）
                        int windowSize = 20;
                        int start = page - windowSize / 2;
                        if (start < 1) start = 1;
                        int end = start + windowSize - 1;
                        if (end > totalPages)
                        {
                            end = totalPages;
                            start = Math.Max(1, end - windowSize + 1);
                        }
                        ViewBag.PageStart = start;
                        ViewBag.PageEnd = end;
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT Id, Name, Hp, Attack, Defense, SpAtk, SpDef, Speed, Height, Weight, Abilities FROM Pokemon WHERE Name LIKE @name ORDER BY {orderCol} {dir} LIMIT @limit OFFSET @offset";
                cmd.Parameters.AddWithValue("@name", "%" + q.Trim() + "%");
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var id2 = rdr.GetInt32(0);
                    var name = rdr.IsDBNull(1) ? "(null)" : rdr.GetString(1);
                    int? hp = rdr.IsDBNull(2) ? null : (int?)rdr.GetInt32(2);
                    int? attack = rdr.IsDBNull(3) ? null : (int?)rdr.GetInt32(3);
                    int? defense = rdr.IsDBNull(4) ? null : (int?)rdr.GetInt32(4);
                    int? spatk = rdr.IsDBNull(5) ? null : (int?)rdr.GetInt32(5);
                    int? spdef = rdr.IsDBNull(6) ? null : (int?)rdr.GetInt32(6);
                    int? speed = rdr.IsDBNull(7) ? null : (int?)rdr.GetInt32(7);
                    int? height = rdr.IsDBNull(8) ? null : (int?)rdr.GetInt32(8);
                    int? weight = rdr.IsDBNull(9) ? null : (int?)rdr.GetInt32(9);
                    string? abilities = rdr.IsDBNull(10) ? null : rdr.GetString(10);
                    list.Add((id2, name, hp, attack, defense, spatk, spdef, speed, height, weight, abilities));
                }
            }

            return View(list);
        }

        // MVC 詳細頁
        public IActionResult Details(int id)
        {
            var dbPath = FindDatabasePath();
            if (string.IsNullOrEmpty(dbPath)) return NotFound("資料庫不存在");

            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Type1, Type2, Hp, Attack, Defense, SpAtk, SpDef, Speed, Height, Weight, Abilities, Moves FROM Pokemon WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read()) return NotFound();

            var model = new Dictionary<string, object?>();
            model["Id"] = rdr.GetInt32(0);
            model["Name"] = rdr.IsDBNull(1) ? null : rdr.GetString(1);
            model["Type1"] = rdr.IsDBNull(2) ? null : rdr.GetString(2);
            model["Type2"] = rdr.IsDBNull(3) ? null : rdr.GetString(3);
            model["Hp"] = rdr.IsDBNull(4) ? null : rdr.GetInt32(4);
            model["Attack"] = rdr.IsDBNull(5) ? null : rdr.GetInt32(5);
            model["Defense"] = rdr.IsDBNull(6) ? null : rdr.GetInt32(6);
            model["SpAtk"] = rdr.IsDBNull(7) ? null : rdr.GetInt32(7);
            model["SpDef"] = rdr.IsDBNull(8) ? null : rdr.GetInt32(8);
            model["Speed"] = rdr.IsDBNull(9) ? null : rdr.GetInt32(9);
            model["Height"] = rdr.IsDBNull(10) ? null : rdr.GetInt32(10);
            model["Weight"] = rdr.IsDBNull(11) ? null : rdr.GetInt32(11);
            model["Abilities"] = rdr.IsDBNull(12) ? null : rdr.GetString(12);
            model["Moves"] = rdr.FieldCount > 13 && !rdr.IsDBNull(13) ? rdr.GetString(13) : null;

            return View(model);
        }

        // GET: Delete confirmation
        [HttpGet]
        public IActionResult Delete(int id)
        {
            var dbPath = FindDatabasePath();
            if (string.IsNullOrEmpty(dbPath)) return NotFound("資料庫不存在");

            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name FROM Pokemon WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read()) return NotFound();
            var model = new Dictionary<string, object?>();
            model["Id"] = rdr.GetInt32(0);
            model["Name"] = rdr.IsDBNull(1) ? null : rdr.GetString(1);
            return View(model);
        }

        // POST: Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var dbPath = FindDatabasePath();
            if (string.IsNullOrEmpty(dbPath)) return BadRequest("db not found");

            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Pokemon WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            return RedirectToAction(nameof(Index));
        }

        // API: 返回 JSON
        [HttpGet("/api/pokemon/{id}")]
        public IActionResult ApiGet(int id)
        {
            var dbPath = FindDatabasePath();
            if (string.IsNullOrEmpty(dbPath)) return NotFound(new { error = "db not found" });

            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Type1, Type2, Hp, Attack, Defense, SpAtk, SpDef, Speed, Height, Weight, Abilities, Moves FROM Pokemon WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read()) return NotFound(new { error = "not found" });

            var obj = new Dictionary<string, object?>();
            obj["id"] = rdr.GetInt32(0);
            obj["name"] = rdr.IsDBNull(1) ? null : rdr.GetString(1);
            obj["type1"] = rdr.IsDBNull(2) ? null : rdr.GetString(2);
            obj["type2"] = rdr.IsDBNull(3) ? null : rdr.GetString(3);
            obj["hp"] = rdr.IsDBNull(4) ? null : rdr.GetInt32(4);
            obj["attack"] = rdr.IsDBNull(5) ? null : rdr.GetInt32(5);
            obj["defense"] = rdr.IsDBNull(6) ? null : rdr.GetInt32(6);
            obj["sp_atk"] = rdr.IsDBNull(7) ? null : rdr.GetInt32(7);
            obj["sp_def"] = rdr.IsDBNull(8) ? null : rdr.GetInt32(8);
            obj["speed"] = rdr.IsDBNull(9) ? null : rdr.GetInt32(9);
            obj["height"] = rdr.IsDBNull(10) ? null : rdr.GetInt32(10);
            obj["weight"] = rdr.IsDBNull(11) ? null : rdr.GetInt32(11);
            obj["abilities"] = rdr.IsDBNull(12) ? null : rdr.GetString(12);
            obj["moves"] = rdr.IsDBNull(13) ? null : rdr.GetString(13);

            return Json(obj);
        }

        // GET: Upsert (create or edit)
        [HttpGet]
        public IActionResult Upsert(int? id)
        {
            var dbPath = FindDatabasePath();
            if (string.IsNullOrEmpty(dbPath))
            {
                ViewBag.Message = "找不到資料庫，無法編輯或新增";
                return View(new Models.PokemonEditModel());
            }

            if (!id.HasValue)
            {
                return View(new Models.PokemonEditModel());
            }

            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Type1, Type2, Hp, Attack, Defense, SpAtk, SpDef, Speed, Height, Weight, Abilities, Moves FROM Pokemon WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id.Value);
            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read()) return NotFound();

            var m = new Models.PokemonEditModel();
            m.Id = rdr.GetInt32(0);
            m.Name = rdr.IsDBNull(1) ? null : rdr.GetString(1);
            m.Type1 = rdr.IsDBNull(2) ? null : rdr.GetString(2);
            m.Type2 = rdr.IsDBNull(3) ? null : rdr.GetString(3);
            m.Hp = rdr.IsDBNull(4) ? null : (int?)rdr.GetInt32(4);
            m.Attack = rdr.IsDBNull(5) ? null : (int?)rdr.GetInt32(5);
            m.Defense = rdr.IsDBNull(6) ? null : (int?)rdr.GetInt32(6);
            m.SpAtk = rdr.IsDBNull(7) ? null : (int?)rdr.GetInt32(7);
            m.SpDef = rdr.IsDBNull(8) ? null : (int?)rdr.GetInt32(8);
            m.Speed = rdr.IsDBNull(9) ? null : (int?)rdr.GetInt32(9);
            m.Height = rdr.IsDBNull(10) ? null : (int?)rdr.GetInt32(10);
            m.Weight = rdr.IsDBNull(11) ? null : (int?)rdr.GetInt32(11);
            m.Abilities = rdr.IsDBNull(12) ? null : rdr.GetString(12);
            m.Moves = rdr.FieldCount > 13 && !rdr.IsDBNull(13) ? rdr.GetString(13) : null;

            return View(m);
        }

        // POST: Upsert
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Upsert(Models.PokemonEditModel model)
        {
            var dbPath = FindDatabasePath();
            if (string.IsNullOrEmpty(dbPath)) return BadRequest("db not found");

            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();

            if (model.Id > 0)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"UPDATE Pokemon SET Name=@name, Type1=@t1, Type2=@t2, Hp=@hp, Attack=@atk, Defense=@def, SpAtk=@spa, SpDef=@spd, Speed=@spd2, Height=@h, Weight=@w, Abilities=@ab, Moves=@mv WHERE Id=@id";
                cmd.Parameters.AddWithValue("@name", (object?)model.Name ?? System.DBNull.Value);
                cmd.Parameters.AddWithValue("@t1", (object?)model.Type1 ?? System.DBNull.Value);
                cmd.Parameters.AddWithValue("@t2", (object?)model.Type2 ?? System.DBNull.Value);
                cmd.Parameters.AddWithValue("@hp", model.Hp.HasValue ? (object)model.Hp.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@atk", model.Attack.HasValue ? (object)model.Attack.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@def", model.Defense.HasValue ? (object)model.Defense.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@spa", model.SpAtk.HasValue ? (object)model.SpAtk.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@spd", model.SpDef.HasValue ? (object)model.SpDef.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@spd2", model.Speed.HasValue ? (object)model.Speed.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@h", model.Height.HasValue ? (object)model.Height.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@w", model.Weight.HasValue ? (object)model.Weight.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@ab", (object?)model.Abilities ?? System.DBNull.Value);
                cmd.Parameters.AddWithValue("@mv", (object?)model.Moves ?? System.DBNull.Value);
                cmd.Parameters.AddWithValue("@id", model.Id);
                cmd.ExecuteNonQuery();
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
            else
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO Pokemon (Name, Type1, Type2, Hp, Attack, Defense, SpAtk, SpDef, Speed, Height, Weight, Abilities, Moves) VALUES (@name,@t1,@t2,@hp,@atk,@def,@spa,@spd,@spd2,@h,@w,@ab,@mv); SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@name", (object?)model.Name ?? System.DBNull.Value);
                cmd.Parameters.AddWithValue("@t1", (object?)model.Type1 ?? System.DBNull.Value);
                cmd.Parameters.AddWithValue("@t2", (object?)model.Type2 ?? System.DBNull.Value);
                cmd.Parameters.AddWithValue("@hp", model.Hp.HasValue ? (object)model.Hp.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@atk", model.Attack.HasValue ? (object)model.Attack.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@def", model.Defense.HasValue ? (object)model.Defense.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@spa", model.SpAtk.HasValue ? (object)model.SpAtk.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@spd", model.SpDef.HasValue ? (object)model.SpDef.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@spd2", model.Speed.HasValue ? (object)model.Speed.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@h", model.Height.HasValue ? (object)model.Height.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@w", model.Weight.HasValue ? (object)model.Weight.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@ab", (object?)model.Abilities ?? System.DBNull.Value);
                cmd.Parameters.AddWithValue("@mv", (object?)model.Moves ?? System.DBNull.Value);
                var inserted = cmd.ExecuteScalar();
                int newId = 0;
                if (inserted != null && int.TryParse(inserted.ToString(), out var nid)) newId = nid;
                return RedirectToAction(nameof(Details), new { id = newId == 0 ? (object?)null : newId });
            }
        }
    }
}
