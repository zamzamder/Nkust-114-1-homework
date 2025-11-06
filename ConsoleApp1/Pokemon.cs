public class PokemonListResponse
{
    public List<PokemonListItem> results { get; set; }
}

public class PokemonListItem
{
    public string name { get; set; }
    public string url { get; set; }
}

public class PokemonRaw
{
    public int id { get; set; }
    public string name { get; set; }
    public List<TypeWrapper> types { get; set; }
    public int height { get; set; }
    public int weight { get; set; }
    public List<StatWrapper> stats { get; set; }
    public List<AbilityWrapper> abilities { get; set; }
    public List<MoveWrapper> moves { get; set; }
}

public class TypeWrapper
{
    public TypeInfo type { get; set; }
}

public class TypeInfo
{
    public string name { get; set; }
}

public class StatWrapper
{
    public int base_stat { get; set; }
    public StatInfo stat { get; set; }
}

public class StatInfo
{
    public string name { get; set; }
}

public class AbilityWrapper
{
    public AbilityInfo ability { get; set; }
}

public class AbilityInfo
{
    public string name { get; set; }
}

public class MoveWrapper
{
    public MoveInfo move { get; set; }
}

public class MoveInfo
{
    public string name { get; set; }
}

// ✅ 清理後格式：用於分析
public class PokemonClean
{
    public int id { get; set; }
    public string name { get; set; }
    public string type1 { get; set; }
    public string type2 { get; set; }

    public int hp { get; set; }
    public int attack { get; set; }
    public int defense { get; set; }
    public int sp_atk { get; set; }
    public int sp_def { get; set; }
    public int speed { get; set; }

    public int height { get; set; }
    public int weight { get; set; }

    public string abilities { get; set; }
    public string moves { get; set; }
}
