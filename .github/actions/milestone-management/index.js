const core = require('@actions/core');
const github = require('@actions/github');

async function run() {
    try {
        const owner = 'winsw';
        const repo = 'winsw';

        const oldTitle = core.getInput('old');
        const newTitle = core.getInput('new');

        const repoToken = core.getInput('repo-token');
        const octokit = github.getOctokit(repoToken);

        const milestones = await octokit.issues.listMilestones({
            owner: owner,
            repo: repo,
        });

        const oldMilestone = milestones.data.find(milestone => milestone.title === oldTitle);

        const newMilestone = await octokit.issues.createMilestone({
            owner: owner,
            repo: repo,
            title: newTitle,
            state: 'closed',
        });

        const issuesToUpdate = await octokit.issues.listForRepo({
            owner: owner,
            repo: repo,
            milestone: oldMilestone.number,
            state: 'closed',
        });

        for (const issue of issuesToUpdate.data) {
            await octokit.issues.update({
                owner: owner,
                repo: repo,
                issue_number: issue.number,
                milestone: newMilestone.data.number,
            });
        }
    }
    catch (error) {
        core.setFailed(error.message);
    }
}

run();
