this guide will be mostly a mix of the original one, and some things i added.

(original guide by <a href="https://github.com/SrCookie450">SrCookie450</a>, changed and site fixed by <a href="https://github.com/harryzawg">harryzawg</a>)
## things you need

- <a href="https://nodejs.org/dist/v18.16.1/node-v18.16.1-x64.msi">Node.js</a>, *to run the renderer/build panel*
- <a href="https://sbp.enterprisedb.com/getfile.jsp?fileid=1258627">PostgreSQL</a>, *for database*
- <a href="https://builds.dotnet.microsoft.com/dotnet/Sdk/6.0.412/dotnet-sdk-6.0.412-win-x64.exe">.NET 6.0</a>, *to run the website*
- <a href="https://go.dev/dl/go1.20.6.windows-amd64.msi">Go</a>, *for asset validation*

## important

- open Command Prompt, and cd into your PostgreSQL folder. it should be at ```C:\Program Files\PostgreSQL\(your postgres version, if you followed the guide it will be 13)\bin```
- copy the schema.sql file in ```services/api/sql``` to that PostgreSQL bin folder, then run

```psql --username=yourusername --dbname=yourdatabase < schema.sql```

- cd into ```services/Roblox/Roblox.Website```, 
- rename the ```appsettings.example.json``` file to just ```appsettings.json```, then open it.
- change the default POSTGRES line that looks like this:
 ```"Postgres": "Host=127.0.0.1; Database=bubbabloxnew; Password=test; Username=postgres; Maximum Pool Size=20",``` 
and change that to:

``` "Postgres": "Host=127.0.0.1; Database=The database you want to use, if you want to use the default one, make this 'postgres'; Password=your Postgres password you set in the setup; Username=postgres; Maximum Pool Size=20",```

- press ```CTRL + H``` and change ```C:\\Users\\Admin\\Desktop\\Revival\\ecsr\\ecsrev-main\\services\\``` to ```C:\\whereever your ECS folder is\\services\\```

**site is mostly set up!**

- go into ```services/api```, 
- create a folder named ```storage```.
- inside the ```storage``` folder, make a folder named ```asset``` 
- go to ```services/api/public/images``` make a folder named ```thumbnails``` and ```groups```
- open Command Prompt and cd into ```services/admin```, then run ```npm i``` and ```npm run build```
- go to ```services/2016-roblox-main``` and rename the file named config.example.json to config.json.
- replace ```your.domain``` with your actual domain inside of that config.json file.
- in a Command Prompt window, cd into that same folder (```2016-roblox-main```), do ```npm i``` and then ```npm run build```
- go to ```services/renderer```, rename the file named ```config.example.json``` to ```config.json``` and change everything inside of it.

## replacing URLs

- go into ```services/renderer/scripts```
- go to the scripts folder, go into the ```player``` folder and in each file, replace ```http://bb.zawg.ca``` and ```https://bb.zawg.ca``` with your domain.
- in the ```asset``` folder's files, do the same thing. make sure to not replace the ```http``` and ```https```, if something is HTTP keep it that way.

Now, go back into the renderer and run ```npm i```, then ```npm run build```

## discord

- go to the <a href="https://discord.com/developers/applications">Discord Developer Portal</a> and make a new application.
- go into OAuth2, and replace the client id in the appsettings.json with your new client ID and client secret.
- add your redirect URL under the client ID section to be ```https://your.domain/discordcb```, replacing your.domain with your domain. do the same for ```https://your.domain/forgotcb```
- update the client ID, secret and redirect URL in ```appsettings.json``` or else it won't work.

## almost there!

- download [HxD](https://mh-nexus.de/en/downloads.php?product=HxD20) and drag the RCCService.exe file into it. make sure the domain you are using for this is exactly 10 characters, or it won't work correctly without a workaround (provided below).
- the reason for this is the way that RCC was compiled, it was set to use Roblox's domain which is 10 characters. just replace it with your 10 char domain (CTRL + R, then do bb.zawg.ca then replace it with your domain. make sure your direction is all)
- do the same for the client. change your public key (in Roblox/Roblox.Website/PublicKey and your private key, everything related to it. You can easily find guides/tools for it. If you do, do the same for the webserver.)
- also, change the domain in AppSettings.xml to your domain. (for client and RCC)

## if you don't have a domain, or a domain with 10 characters..

- open ```C:\Windows\System32\drivers\etc\hosts``` in an elevated (running as admin) Notepad.
- once in the file in an elevated Notepad, put something like ```127.0.0.1 your10chardomain```. it can be anything, but it must be 10 characters
- continue to patch the RCC with that 10 character domain.

## the site should be setup at this point!
- go into ```/services```, run ```runall.bat```, when it's all done go to your site at localhost:90.
- sign up for an account with the name ```ROBLOX```, then go to https://your.domain/admin, and go to create player under Users, put ID 2500, the name as ```UGC``` and a random password, then go to that user on the admin panel and click Nullify Password.
- go back to Create Player and set the ID to 12, and the name as ```BadDecisions```, have common sense when making the password for this account.
- now, sign up with your account normally.

**congrats, site made!**

## webserver

- change the directory root in ```webserver\apache\conf\extra\httpd-vhosts.conf``` to your actual webserver root location.
- update everything in ```webserver\apache\conf\httpd.conf``` to your actual server root and directory locations.
- go into ```webserver/root/game``` then go into join.ashx and change the bs.zawg.ca and sitetest.zawg.ca URL's to your website URL.
- do the same for PlaceLauncher.ashx and the asset endpoints, so it can actually get assets from your site in game.
- you should now be able to start the webserver, and connect using the client.

## client

- start the webserver
- patch the client in HxD, the same way as RCC, then go to /game/get-join-script?placeid=(the place you want to join)
- then go to the client's directory in CMD using CD, then do CLIENTNAME.exe (paste everything in the get join script endpoint after the client exe)
