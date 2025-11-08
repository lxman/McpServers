# McpServers

This project is a personally curated set of tools based on my last six months of Claude. I've found it to be a valuable tool and these tools are designed to extend his usefulness to enterprise developers.

## Why did I choose this particular design setup?

The particular setup was based on a few design decisions:

1. **Why C#?** - Because I'm a .NET kinda guy. I'm iconoclastic like that.
2. **Why these servers instead any of the thousands of others in existence?** - Good question. I've tried some of the others and I wasn't happy with some of the limitations or deficiencies that they had, some by design and some not. I have to explain that these servers don't have the guard rails that I have seen in others. There are directory restrictions but Claude can overwrite those if he feels the need. Some of the most dangerous commands are blacklisted (`rm -rf`, `format c` and stuff like that - Claude cannot bypass these) but otherwise I consider that my relationship with Claude has worked fairly smoothly. If I don't want him to start modifying code on disk I just tell him not to and 99% of the time he respects that. It's always best to have a clean git commit in place before you start working an idea with Claude anyway just in case he does decide to go off the rails. So, in short, these guard rails work for me. YMMV
2. **Why this particular server arrangement?** - In designing these tools I originally set them all up as separate STDIO MCP servers. Those are convenient because Claude initializes them when it starts up and automatically receives a description of the tools, what they do, how to use them, etc. That sounds convenient, and it is, but there's a cost there that I discovered. Everyone using an AI these days for development has one overarching concern while doing so, token consumption. You're always fighting to either stay under your limit or minimize how many you use. I have the top of the line Anthropic subscription for my personal use and with Sonnet 4.5 I am allowed 190k tokens per conversation. My conversations were being cut off sooner and sooner and after discussing it with Claude I finally discovered why. Reading the initial instructions and descriptions of all of these MCP servers was costing about 100k tokens. Over half of my budget was burned up with Claude just getting to know how to use the tools I was providing him. So the architecture that I finally decided on is based on a fairly new concept in the AI world. Anthropic's version of it is called Agent Skills. It's not really a new-fangled thing. It's just a different viewpoint. Instead of tagging all of the tools and their parameters you simply add a very brief description with a "for more information see . . ." and then construct a hierarchy of `md` files with the bulk of the information, examples, etc. That way relatively few tokens are spent reading the original descriptions and we only spend the cost per tool as Claude decides that it needs to use them. This defers the expense nicely.
3. **Why Go, Python and Typescript?** - Those particular tools are for the analysis of those particular kinds of code. It's a bit janky to have C# tools try to analyze another language's code. While it can be done I find the path much more straightforward to use Go to analyze Go, Python to analyze Python, etc. Fortunately with Claude working with other languages is much simpler.

I think that explains the main reasons for what some may consider an unusual design philosophy.To set this up with Claude, just add some sections to your `claude_desktop_config.json` (1 for each server):

```aiignore
"desktop-commander": {
  "command": "dotnet",
  "args": [
    "<path-to-the-compiled-dll-for-DesktopCommanderMcp>"
  ]
}
```

and boot Claude.

Basically the process should be fairly painless to set up. Just download the repo, compile everything (I prefer Release mode) and fill in `claude_desktop_config.json` or `C:\Users\xxx\.claude.json` if you're using Claude Code.

The Go, Python and Typescript servers are a bit different but there is a `run.bat` in each folder that can provide some assistance in setting up each project.

Cross-platform? I'm not actually sure. It seems I recall throwing a few Windows-specific bits in there so you would have to try it and see.

And of course, this is an ongoing project, so there is no guarantee on breaking changes.