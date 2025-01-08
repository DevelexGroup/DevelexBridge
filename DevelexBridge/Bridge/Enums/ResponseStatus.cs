using System.ComponentModel.DataAnnotations;

namespace Bridge.Enums;

public enum ResponseStatus
{
    [Display(Name = "processing")]
    Processing,
    
    [Display(Name = "rejected")]
    Rejected,
    
    [Display(Name = "resolved")]
    Resolved
}