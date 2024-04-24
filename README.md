# AWSIM Labs

<img src="docs/assets/images/E2ESim.png" height="300">

<img src="docs/assets/images/autoware-foundation.png" height="90"> <img src="docs/assets/images/awsim-labs-logo.png" height="90">

[AWSIM Labs](https://github.com/autowarefoundation/AWSIM) is currently being developed under the [Autoware Labs](https://github.com/orgs/autowarefoundation/discussions/4550) initiative. Main purpose of this fork is to provide faster implementation of features needed by the users of the AWSIM while also ensuring a high-performance simulation environment for the [Autoware](https://github.com/autowarefoundation/autoware).

This is a fork of [TIER IV's AWSIM](https://github.com/tier4/AWSIM).

## Features

- Simulator components included (Vehicle, Sensor, Environment, ROS2, etc.)
- Support for Ubuntu 22.04 and windows10/11
- ROS2 native communication
- Open source software
- Made with Unity Game Engine
- Multiple scene and vehicle setup
- Interactable simulation and UI

### Feature differences from the TIER IV/AWSIM

| Features                                  | AWSIM 1.2.1      | AWSIM Labs 1.0.0-beta |
|-------------------------------------------|------------------|-----------------------|
| Rendering Pipeline                        | HDRP             | URP                   |
| Unity Version                             | Unity 2021.1.7f1 | Unity LTS 2022.3.21f1 |
| Resource usage                            | Heavy            | Light                 |
| Can reset vehicle position on runtime     | ❌                | ✅                     |
| Multiple scene and vehicle setup          | ❌                | ✅                     |
| Multi-lidars are enabled by default       | ❌                | ✅                     |
| CI for build                              | ❌                | ✅                     |
| CI for documentation generation within PR | ❌                | ✅                     |

## Tutorial

First, try the tutorial!  
[AWSIM Labs Documentation - Quick Start Demo](https://autowarefoundation.github.io/AWSIM/main/GettingStarted/QuickStartDemo/)

## Documentation

https://autowarefoundation.github.io/AWSIM/main/

## How to Contribute

Everyone is welcome!
1. Create a issue [here](https://github.com/autowarefoundation/AWSIM/issues) to discuss the contribution you want to make.
2. Create a derived branch `feature/***` from the `main` branch.
3. Create a pull request for the `main` branch.

see also [AWSIM Labs Documentation - Git branch](https://autowarefoundation.github.io/AWSIM/main/ProjectGuide/GitBranch/)

We recommend [microsoft's C# coding convention](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions?redirectedfrom=MSDN).  
However, if the logic of the code is good, it does not matter if coding conventions are not followed.

## License

AWSIM License
Applies to `tier4/AWSIM` repositories and all content contained in the [Releases](https://github.com/autowarefoundation/AWSIM/releases).

- code : Apache 2.0
- assets : CC BY-NC

See also [LICENSE](./LICENSE)

## Contact

日本語/English OK

e-mail : takatoki.makino@tier4.jp
discord : mackie#6141
twitter : https&#58;//twitter.com/mackierx111

(c) 2022 TIER IV, inc
