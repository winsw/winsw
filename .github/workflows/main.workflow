workflow "Push" {
  on = "push"
  resolves = ["Draft Release"]
}

action "Draft Release" {
  uses = "toolmantim/release-drafter@v5.2.0"
  secrets = ["GITHUB_TOKEN"]
}
