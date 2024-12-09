namespace LastMinutes.Models.Shared;

public class LayoutViewModel
{

    public bool SignedIn { get; set; } 
    
    public string Name { get; set; }


    public LayoutViewModel(string name = "")
    {
        Name = name;
        
        if (!string.IsNullOrEmpty(name))
        {
            SignedIn = true;
        }
    }
}