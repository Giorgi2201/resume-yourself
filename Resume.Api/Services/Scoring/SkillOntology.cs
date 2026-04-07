namespace Resume.Api.Services.Scoring;

public static class SkillOntology
{
    // Canonical skill -> aliases/synonyms/abbreviations
    public static readonly Dictionary<string, string[]> CanonicalSkills = new(StringComparer.OrdinalIgnoreCase)
    {
        ["javascript"] = ["javascript", "js", "ecmascript"],
        ["typescript"] = ["typescript", "ts"],
        ["html"] = ["html", "html5"],
        ["css"] = ["css", "css3", "scss", "sass"],
        ["wordpress"] = ["wordpress", "wp", "woocommerce"],
        ["react"] = ["react", "reactjs", "react.js"],
        ["angular"] = ["angular", "angular2", "angular 2+"],
        ["vue"] = ["vue", "vuejs", "vue.js"],
        ["node.js"] = ["node", "nodejs", "node.js"],
        ["asp.net"] = ["asp.net", "aspnet", ".net web", "dotnet web"],
        ["sql"] = ["sql", "mysql", "postgresql", "postgres", "mssql", "sql server", "sqlite"],
        ["rest api"] = ["rest", "rest api", "restful api"],
        ["graphql"] = ["graphql", "gql"],
        ["docker"] = ["docker", "containerization"],
        ["kubernetes"] = ["kubernetes", "k8s"],
        ["aws"] = ["aws", "amazon web services"],
        ["azure"] = ["azure", "microsoft azure"],
        ["gcp"] = ["gcp", "google cloud", "google cloud platform"],
        ["python"] = ["python", "py"],
        ["java"] = ["java"],
        ["c#"] = ["c#", "csharp", "dotnet"],
        ["go"] = ["go", "golang"],
        ["pandas"] = ["pandas"],
        ["numpy"] = ["numpy", "np"],
        ["machine learning"] = ["machine learning", "ml"],
        ["deep learning"] = ["deep learning", "dl"],
        ["seo"] = ["seo", "search engine optimization"],
        ["ppc landing pages"] = ["ppc landing page", "ppc landing pages", "paid landing pages", "landing pages"],
        ["cro"] = ["cro", "conversion optimization", "conversion rate optimization"],
        ["page speed optimization"] = ["page speed optimization", "pagespeed optimization", "core web vitals", "lighthouse optimization"],
        ["google tag manager"] = ["google tag manager", "gtm"],
        ["ga4"] = ["ga4", "google analytics 4", "google analytics"],
        ["mobile-first design"] = ["mobile-first design", "mobile first design", "responsive design", "responsive ui"],
        ["figma"] = ["figma"],
        ["jira"] = ["jira"],
        ["git"] = ["git", "github", "gitlab", "bitbucket"],
    };

    // Related skills: weaker but still relevant if canonical not directly present
    public static readonly Dictionary<string, string[]> RelatedSkills = new(StringComparer.OrdinalIgnoreCase)
    {
        ["javascript"] = ["typescript"],
        ["typescript"] = ["javascript"],
        ["cro"] = ["a/b testing", "ab testing", "funnel optimization"],
        ["ppc landing pages"] = ["landing page", "paid media", "google ads"],
        ["ga4"] = ["google tag manager"],
        ["google tag manager"] = ["ga4"],
        ["mobile-first design"] = ["responsive design"],
        ["rest api"] = ["http api"],
    };

    public static IEnumerable<string> AllCanonicals => CanonicalSkills.Keys;
}

