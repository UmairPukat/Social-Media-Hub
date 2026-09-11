using System.ComponentModel.DataAnnotations;

namespace SocialMedia.Application.DTOs.Meta;

public class MetaCatalogDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Vertical { get; set; }
    public string? ProductCount { get; set; }
    public string? BusinessId { get; set; }
    public string? Source { get; set; }
}

public class MetaProductDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? RetailerId { get; set; }
    public string? Price { get; set; }
    public string? Currency { get; set; }
    public string? Availability { get; set; }
    public string? ImageUrl { get; set; }
    public string? Url { get; set; }
    public string? ReviewStatus { get; set; }
}

public class MetaCatalogListQuery
{
    public string? After { get; set; }
    public int Limit { get; set; } = 25;
}

public class CreateMetaCatalogRequest
{
    public string? BusinessId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = "App Review Demo Catalog";

    public string Vertical { get; set; } = "commerce";
}

public class MetaProductListQuery
{
    [Required]
    public string CatalogId { get; set; } = string.Empty;

    public string? Search { get; set; }
    public string? After { get; set; }
    public int Limit { get; set; } = 25;
}

public class CreateMetaProductRequest
{
    [Required]
    public string CatalogId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string RetailerId { get; set; } = string.Empty;

    [Required]
    public string Price { get; set; } = "9.99";

    public string Currency { get; set; } = "USD";

    public string Availability { get; set; } = "in stock";

    public string? Url { get; set; }

    public string? ImageUrl { get; set; }

    public string? Description { get; set; }
}

public class UpdateMetaProductRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    public string? Price { get; set; }

    public string? Currency { get; set; }

    public string? Availability { get; set; }

    public string? Url { get; set; }

    public string? ImageUrl { get; set; }

    public string? Description { get; set; }
}
