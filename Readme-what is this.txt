========== What is this? =============
BazaarPlannerMod will export game data to the database used by BazaarPlanner, 
clicking "follow" on the website (under the board) will instantly change your board on the site when you change it in game.
You can give your user id (the default that in the field after clicking follow) to a friend if they would like to follow you.
They can follow you on the TOP board, to see how their board vs your board would do.
This also enables the "runs" tab on the website, making it easy to look back and load previous battles to replay,
 or to see how your boards stack up against the top ones submitted. 

While Reynad isn't currently supporting our tool, we hope that he might change his mind, 
and that this feature won't be received negatively by their team. 
Don't talk about your usage of this tool in the official discord, or you may get banned.

Small request - lets stay positive in regards to feedback (and not disparagethe tempo storm staff in any way),
that would be great!They have made a fun game, and we want them to support tools and a community
that make playing the game even more fun. 

Thanks again!

Oh! Also, it enables a 'streamer mode' feature so within the game you will never see your bazaar username on screen; 
In the games, tracked runs, and data on the site, it will use the display name in the config file.

========== Install - Windows ============
1. First, make sure you've started the game and updated to latest version, then close the game.
2. Extract all the files from the bazaarplannermod zip to a directory.
3. Download "BazaarPlanner.config" from https://www.bazaarplanner.com/ and put in that directory from step 2. (see "Generating Config File" below if you are unsure how to do this)
4. Run BazaarPlannerModInstaller.exe and Click install - Set the installation directory to the same location as "TheBazaar.exe".
NOTE: STEAM USERS - This will be something like "C:\Program Files\Steam\steamapps\common\The Bazaar"
The default value is still the previous tempo launcher directory (non steam. you may need to tweak this depending if you installed to different than default)
5. Now you can start the game. Select "Follow" on the website and your board should automatically populate on the website as you play, and runs will be tracked.
NON-STEAM USERS: Only use the old tempo launcher when there's a game update. To launch (and not delete the installation as tempo may detect it and do so, as happens on every game update),
 run customlauncher.bat file to launch the game, using the same user/password as on their website.

========== Generating Config File ============
1. Go to https://www.bazaarplanner.com/
2. Click the cog in the top right corner and log in or make an account as needed
3. Click the cog in the top right corner again.
4. Click "Export BazaarPlanner.config"

========== Manual install (ignore this if you did the install above already) =================================
Without running the .exe - if you don't trust it not to have been tampered with.
Always good to do this when possible, we don't publish with viruses/malware/spyware, but your friend giving this to you might give you a keylogger along with it! If you are going to run an exe make sure its the one linked directly from our site.

1. extract BepInEx_win_x64_5.4.23.2.zip to your game install directory (that contains TheBazaar.exe).
This directory may be different depending on if you are in steam or standalone. The one that works for me is
C:\Users\PC\AppData\Roaming\Tempo Launcher - Beta\game\buildx64

2. create a directory "plugins" inside the BepInEx directory that got created.

3. copy BazaarPlannerMod.dll to C:\Program Files\Tempo Launcher - Beta\The Bazaar game_64\bazaarwinprodlatest\BepInEx\plugins

Starting the game will now create files and allow you to press 'b' to load your board into bazaarplanner.
BUT! There's more. If you want to follow and track your runs, you'll need to continue.

4. Go to bazaarplanner.com and click the gear icon in the top right, login if needed, and click Export BazaarPlanner.config

5. Create a directory "config" inside the BepInEx directory and Copy this config file there.

6. Rename it from BazaarPlanner.config to BazaarPlanner.cfg
(Sometimes windows makes this annoying because it hides file extensions from you (god I hate bill gates for doing this so many years ago). If you start the game and look in this folder and see two different files, Simply take the contents of BazaarPlanner.config and copy them into BazaarPlanner.cfg - you can then delete the .config file)

Done! 
Share and Enjoy!