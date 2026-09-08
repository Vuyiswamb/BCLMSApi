namespace BCLMSApi.Models;
public class FoodVendingDetails
{
    public List<string> BreakfastItems { get; set; } = [];
    public List<string> CookedFoodItems { get; set; } = [];
    public List<string> RefreshmentsItems { get; set; } = [];
    public bool? UsesTrailer { get; set; }
}
