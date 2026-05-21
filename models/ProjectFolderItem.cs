namespace FileCleaner.Models;

public class ProjectFolderItem
{
    public string Name { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string ProjectType { get; set; } = "";
    public string SourceName { get; set; } = "";

    public string TypeDescription => ProjectType switch
    {
        "Git" => "Git 저장소",
        "Unity" => "Unity 프로젝트",
        "VisualStudio" => "Visual Studio/.NET 프로젝트",
        "NodeJS" => "Node.js 프로젝트",
        "Python" => "Python 프로젝트",
        "Rust" => "Rust 프로젝트",
        "Go" => "Go 프로젝트",
        "Maven" => "Maven/Java 프로젝트",
        "Gradle" => "Gradle 프로젝트",
        "CMake" => "C/C++ CMake 프로젝트",
        "Make" => "Make 기반 프로젝트",
        "PHP" => "PHP Composer 프로젝트",
        "Ruby" => "Ruby 프로젝트",
        "Flutter" => "Flutter/Dart 프로젝트",
        _ => string.IsNullOrWhiteSpace(ProjectType) ? "프로젝트" : $"{ProjectType} 프로젝트"
    };
}
