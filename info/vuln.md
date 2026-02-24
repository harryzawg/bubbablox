## Addressing vulnerability stuff
 - There are no site vulnerabilities. Anything you saw about any item vuln was just an api I forgot to remove before publishing. Please don't listen to people

 ## ITEMS
 - Originally, when bubbablox was still around we had an Egg Hunt event. I made a simple api for it to just grant items through the api because that was the easiest thing to do at the time. After the event was over, I never removed the api and people found it and began exploiting it and claiming it was a vulnerability. They were able to use it because the apikey in it was static and not set through appsettings because I planned for it to just be a temporary API for while the event was running but just forgot about it. Sorrrryy whoever was affected, If I find anymore apis like that I will update the repo

 ## People leaving RCC ports and running jobs without trust check
 - The clients and RCC that are in this repo DO have trust check. If you find any server with open RCC ports and no trust check just know they are probably stupid and don't know how to properly configure the RCC because all the RCCS in this repo do have trust check, if you don't believe me just open it in a debugger the trust checks are not jmped, also if you are hosting please close your db ports you fucking buffoons how stupid are you to keep your db ports open

 ## Login vuln
 - Not really a vuln but I saw people talking about my ID being in the discord login endpoint, again it was just a temp testing thing that I forgot about and I never used it so