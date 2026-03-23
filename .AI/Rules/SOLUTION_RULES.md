- each project will get its own living readme that gets update as the project changes
- each project will have its on Rules.md that explains that projects rules more specifically.  It can add rules, or supercede SOLUTION_RULES.md
- all services, VMs, and DAs must implement structured logging using Serilog
- all Services, VMS, and DAs must follow patterns inside their corresponding .AI/Rules file

Fold structure:
/Application should contain all application wide items
/Areas will contain the bulk of the UI and inside will have a folder for each logical groupiing of pages

Example Area folder structure for a Users area
/Areas
  /Users
    /Index
      /Components
      UsersIndexPage.razor
      UsersIndexPageVM
      UsersIndexService.cs
      UsersIndexServiceDA.cs
    /Search
      /Components
      UsersSearchPage.razor
      UsersSearchPageVM
      UsersSearchService.cs
      UsersSearchServiceDA.cs
    //Shared (componets used in multipe areas)

    