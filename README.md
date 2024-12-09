# Last Minutes
Last Minutes is a tool that finds out how many minutes you've listened to in total on Last.FM  
Contributors get to add their Last.FM username to the `LastMinutes/AppData/specialAccounts.json` list.


# Requirements
- .Net 6
- Microsoft SQL Server (2019 or later)
- API key and client secret for a Spotify application
- API key for Last.FM

# Running
Last Minutes is built on .Net 6 and is an MVC app. It heavily depends on an MSSQL server to store data such as queue items, track cache and results. To start, create an SQL server and table, then generate a connection string and add it to `dataSettings.json`. Then you will need to run `dotnet ef database update` in a terminal, which will run the app and create all the database tables from the migrations folder.
