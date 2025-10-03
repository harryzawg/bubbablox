## please do not contact me on trying to get the source setup. there are many guides on how to and if you read this guide properly you shouldn't need help.
<div align="center">
    <p>
      <h1>BubbaBlox</h1>
    </p>
</div>

this guide will be mostly a mix of the original one, and some things i added.

(original guide by <a href="https://github.com/SrCookie450">SrCookie450</a>, changed and site fixed by <a href="https://github.com/harryzawg">harryzawg</a>)

## things you need

- <a href="https://nodejs.org/dist/v18.16.1/node-v18.16.1-x64.msi">Node.js</a>, *to run the renderer/build panel*
- <a href="https://sbp.enterprisedb.com/getfile.jsp?fileid=1258627">PostgreSQL</a>, *for the database*
- <a href="https://builds.dotnet.microsoft.com/dotnet/Sdk/6.0.412/dotnet-sdk-6.0.412-win-x64.exe">.NET 6.0</a>, *to run the website*
- <a href="https://go.dev/dl/go1.20.6.windows-amd64.msi">Go</a>, *for asset validation*

## requirements
- at least Windows 10, Linux is untested as my server is a Windows machine. You should use Wine to run everything if you are using linux (or if running a Debian vps, you can use Proxmox and run a windows VM.)
- a 10 character long domain that supports both HTTP and HTTPS
- knowledge on how things like this work (you should have at least some experience with websites and coding to be able to host this. it's really not hard to set up if you know what you're doing.)

## database

- open Command Prompt, and cd into your PostgreSQL folder. it should be at ```C:\Program Files\PostgreSQL\(your postgres version, if you followed the guide it will be 13)\bin```
- copy the schema.sql file in ```api/sql``` to that PostgreSQL bin folder, then run in a Command Prompt window in that folder:

```psql --username=** --dbname=* < schema.sql```

- ```*``` = the name of the database you want to use, if this is your first time installing, use postgres
- ```**``` = your postgres username, default is postgres if you didn't set any in the setup

## setting up

- rename the ```appsettings.example.json``` file in ```Roblox/Roblox.Website``` to just ```appsettings.json```, then open it.
- change the default POSTGRES line that looks like this:
- ```"Postgres": "Host=127.0.0.1; Database=bubbabloxnew; Password=test; Username=postgres; Maximum Pool Size=20",```

to:

``` "Postgres": "Host=127.0.0.1; Database=*; Password=your Postgres password; Username=**; Maximum Pool Size=20",```

- ```*``` = the name of the database you want to use, if this is your first time installing, use postgres
- ```**``` = your postgres username, default is postgres if you didn't set any in the setup

- press ```CTRL + H``` and change ```C:\\Users\\Admin\\Desktop\\Revival\\ecsr\\ecsrev-main\\services\\``` to ```C:\\whereever your ECS folder is\\``` (make sure it's double slashed! so it should look like ```C:\\folder1\\folder2\\```)
- you should update everything in the appsettings.json file to your configuration.
- you should also rename ```game-servers.example.json``` to just ```game-servers.json```
- go to ```services/renderer```, rename the file named ```config.example.json``` to ```config.json``` and change everything inside of it so it works with your main site and matches your appsettings.json.
you should change GameServerAuthorization and the Authorization under Render in your appsettings.json to the Authorization in your renderer config.json.

## thumbnails and frontend

- first off, go into ```Roblox\Roblox.Website\Middleware\CorsMiddleware.cs```, and replace everything that has ```bbblox.org``` in it with your domain, this is so thumbnails can load.
- go into ```services/api```, 
- create a folder named ```storage```.
- inside the ```storage``` folder, make a folder named ```asset``` 
- go to ```services/api/public/images``` make a folder named ```thumbnails``` and ```groups```
- open Command Prompt and cd into ```services/admin```, then run ```npm i``` and ```npm run build```
- go to ```services/2016-roblox-main``` and rename the file named config.example.json to config.json.
- replace ```your.domain``` with your actual domain inside of that config.json file.
- in a Command Prompt window, cd into that same folder (```2016-roblox-main```), do ```npm i``` and then ```npm run build```
- also in a Command Prompt window, cd into ```renderer```, and do ```npm i``` and then ```npm run build```

## discord

- go to the <a href="https://discord.com/developers/applications">Discord Developer Portal</a> and make a new application.
- go into OAuth2, and replace the client id in the appsettings.json with your new client ID and client secret.
- add your redirect URL under the client ID section to be ```https://your.domain/discordcb```, replacing your.domain with your domain. do the same for ```https://your.domain/forgotcb```, and ```https://your.domain/logincb```.
- update the client ID, secret and add your new redirect URLs that you just added in the portal to ```appsettings.json``` or else it won't work.

## almost done!

- download [HxD](https://mh-nexus.de/en/downloads.php?product=HxD20) and drag the RCCService.exe file into it. make sure the domain you are using for this is exactly 10 characters, or it won't work correctly without a workaround (provided below).
- the reason for this is the way that RCC was compiled, it was set to use Roblox's domain which is 10 characters. just replace it with your 10 char domain (CTRL + R, then do bbblox.org as the string then replace it with your domain. make sure your direction is all)
- do the same for the client.
- also, change the domain in AppSettings.xml to your domain. (for clients and RCC)

## RCC access/settings keys and public keys

- open Registry Editor, and navigate to: ```HKEY_LOCAL_MACHINE -> SOFTWARE -> WOW6432Node``` and go into ROBLOX Corporation and then the Roblox folder. (if they don't exist, create the keys)
- then, inside of the Roblox folder, right click, go into New, and select String Value. Then the name should be AccessKey and set the value to whatever your RccAuthorization is.
- then, make another string value named SettingsKey and set the value to whatever you want. after doing this, you should update AllowedQuietGetJson to have RCCServiceYourSettingsKey and just your settings key, replace YourSettingsKey with your actual settings key.
- then, go into ```Roblox/Roblox.Libraries/Json``` and rename the ```RCCServiceBubbleRev2021RCCIsSoTuff.json``` or whatever it is to RCCServiceYourSettingsKey.json (replace YourSettingsKey with your settings key, once again)

## RCC public keys
- you need to run the Generate.bat file in the ```Roblox/Roblox.Website/RSA```. this will generate the private and public keys.
## RCC
## 2016 and 2018:
- open the RCCService folder, and drag the RCCService.exe into HXD. then do ctrl + f and type BGIAA (direction: All)
- get your generated public key from the script you ran earlier, the file is  ```PublicKey2016.pub```. open in notepad, copy everything in it, and replace everything in HXD from BGIAA to the = sign, then save the file.
- for 2018, you might see multiple keys. just replace the first one.
## 2020
- on 2020, the process is a bit different. do a search for .MIIBI and the first result will be your private key.
- paste everything inside of the generated ```PublicKey2020.pub``` from the first - at BEGIN PUBLIC KEY to the last - at END PRIVATE KEY.
- you will see multiple public keys. you only have to replace the first one, not any others.
## CLIENT
## 2016 and 2018
- do the same thing as the 2016 and 2018 instructions, but just on your client exe
## 2020
- do the same thing as the 2020 instructions also, but just on your client exe

## the site should be setup at this point!

- go into ```/services```, run ```runall.bat```, when it's all done go to your site at your domain.
- sign up for an account with the name ```ROBLOX```, then go to /admin, and go to create player under Users, put ID 2500, the name as ```UGC``` and a random password, then go to that user on the admin panel and click Nullify Password.
- if you sign up and it takes around 15-20 seconds and throws an error, try just logging in with the account you created. this happens because it cannot render your avatar, so this will not happen once your renderer is fixed.
- go back to Create Player and set the ID to 12, and the name as ```BadDecisions```, have common sense when making the password for this account.
- now, sign up with your account normally.

**congrats, site is setup and made!**

## client (game join)

- go to /game/get-join-script?placeid=(the place you want to join)
- then go to the client's directory in CMD using CD, then do CLIENTNAME.exe (paste everything in the get join script endpoint after the client exe)
  
## common RCC/renderer errors

## 2018/2020 rcc crashes when i try to join a game, what do i do:
- one of the reasons is that you do not have 3d acceleration or DirectX installed. try installing DirectX, this should solve the issue.
## the renderer does StartProcessException... after it does ThumbnailGenerator::click:
- you do not have 3d acceleration. you can try using [Mesa](https://github.com/lightningterror/Mesa3D-Windows/releases/tag/mesa-21.0.3) (32 bit) and 
## when i try to join a game, it says ID = 17:
- this COULD be one of many things: you have not forwarded your allowed network ports through your router/firewall, or updated your GSIPAddress (your/your servers public IP) in the appsettings. you should also check if your RCC is starting successfully, if it is and it loads the game, then it's a port/ip issue or something else
## it says CURL error and prints a bunch of text, ini and dmp files, or says 400 Bad Request a bunch of times:
- go to ```%localappdata%\Roblox\logs``` and delete everything, this happens when there are dump files in the directory
## i will add more here if more people have issues.
