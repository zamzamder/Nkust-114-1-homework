using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

class PokemonDb
{
    static readonly string DbRelative = Path.Combine("data", "pokemon.db");

    public static string DbPath => Path.Combine(Environment.CurrentDirectory, DbRelative);

    public static void EnsureCreated()
    {
        var dir = Path.GetDirectoryName(DbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        using var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS Pokemon (
          Id INTEGER PRIMARY KEY,
          Name TEXT,
          Type1 TEXT,
          Type2 TEXT,
          Hp INTEGER,
          Attack INTEGER,
          Defense INTEGER,
          SpAtk INTEGER,
          SpDef INTEGER,
          Speed INTEGER,
          Height INTEGER,
          Weight INTEGER,
          Abilities TEXT,
          Moves TEXT
        );";
        cmd.ExecuteNonQuery();
    }

    public static void SaveList(List<PokemonClean> list)
    {
        using var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        using var tran = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        INSERT OR REPLACE INTO Pokemon (Id,Name,Type1,Type2,Hp,Attack,Defense,SpAtk,SpDef,Speed,Height,Weight,Abilities,Moves)
        VALUES (@id,@name,@t1,@t2,@hp,@atk,@def,@spa,@spd,@spe,@h,@w,@ab,@mv);";

        var idP = cmd.CreateParameter(); idP.ParameterName = "@id"; cmd.Parameters.Add(idP);
        var nameP = cmd.CreateParameter(); nameP.ParameterName = "@name"; cmd.Parameters.Add(nameP);
        var t1P = cmd.CreateParameter(); t1P.ParameterName = "@t1"; cmd.Parameters.Add(t1P);
        var t2P = cmd.CreateParameter(); t2P.ParameterName = "@t2"; cmd.Parameters.Add(t2P);
        var hpP = cmd.CreateParameter(); hpP.ParameterName = "@hp"; cmd.Parameters.Add(hpP);
        var atkP = cmd.CreateParameter(); atkP.ParameterName = "@atk"; cmd.Parameters.Add(atkP);
        var defP = cmd.CreateParameter(); defP.ParameterName = "@def"; cmd.Parameters.Add(defP);
        var spaP = cmd.CreateParameter(); spaP.ParameterName = "@spa"; cmd.Parameters.Add(spaP);
        var spdP = cmd.CreateParameter(); spdP.ParameterName = "@spd"; cmd.Parameters.Add(spdP);
        var speP = cmd.CreateParameter(); speP.ParameterName = "@spe"; cmd.Parameters.Add(speP);
        var hP = cmd.CreateParameter(); hP.ParameterName = "@h"; cmd.Parameters.Add(hP);
        var wP = cmd.CreateParameter(); wP.ParameterName = "@w"; cmd.Parameters.Add(wP);
        var abP = cmd.CreateParameter(); abP.ParameterName = "@ab"; cmd.Parameters.Add(abP);
        var mvP = cmd.CreateParameter(); mvP.ParameterName = "@mv"; cmd.Parameters.Add(mvP);

        foreach (var p in list)
        {
            idP.Value = p.id;
            nameP.Value = (object?)p.name ?? DBNull.Value;
            t1P.Value = (object?)p.type1 ?? DBNull.Value;
            t2P.Value = (object?)p.type2 ?? DBNull.Value;
            hpP.Value = p.hp;
            atkP.Value = p.attack;
            defP.Value = p.defense;
            spaP.Value = p.sp_atk;
            spdP.Value = p.sp_def;
            speP.Value = p.speed;
            hP.Value = p.height;
            wP.Value = p.weight;
            abP.Value = (object?)p.abilities ?? DBNull.Value;
            mvP.Value = (object?)p.moves ?? DBNull.Value;

            cmd.ExecuteNonQuery();
        }

        tran.Commit();
    }
}
