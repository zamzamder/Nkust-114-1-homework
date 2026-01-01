namespace ConsoleApp1.Web.Models
{
    public class PokemonEditModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Type1 { get; set; }
        public string? Type2 { get; set; }
        public int? Hp { get; set; }
        public int? Attack { get; set; }
        public int? Defense { get; set; }
        public int? SpAtk { get; set; }
        public int? SpDef { get; set; }
        public int? Speed { get; set; }
        public int? Height { get; set; }
        public int? Weight { get; set; }
        public string? Abilities { get; set; }
        public string? Moves { get; set; }
    }
}
