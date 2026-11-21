"""Match demonstration motor commands to ML-Agents 1.1's PPO action space."""
from mlagents.trainers.torch_entities.components.bc.module import BCModule
from mlagents.torch_utils import torch
from mlagents.trainers.trajectory import ObsUtil
import math


def repair_legacy_action_head(actor):
    """Convert a checkpoint fitted with the old BC loss to PPO raw units once."""
    distribution = actor.action_model._continuous_distribution
    if not actor.action_model.clip_action or distribution.conditional_sigma:
        raise ValueError('Repair supports the pinned non-tanh, constant-sigma PPO actor only')
    with torch.no_grad():
        distribution.mu.weight.mul_(3)
        distribution.mu.bias.mul_(3)
        distribution.log_sigma.add_(math.log(3))


class SensorBCModule(BCModule):
    def _update_batch(self, mini_batch_demo, n_sequences):
        # Real sensor16 observations always contain either an IMU validity bit
        # or a nonzero missing/stale age. All-zero vectors are sequence padding.
        vectors = mini_batch_demo[ObsUtil.get_name_at(1)].to_ndarray()
        self._sample_mask = torch.as_tensor(vectors).abs().sum(dim=1).ne(0)
        try:
            return super()._update_batch(mini_batch_demo, n_sequences)
        finally:
            self._sample_mask = None

    def _behavioral_cloning_loss(self, selected_actions, log_probs, expert_actions):
        # Non-tanh PPO sends clip(raw_action,-3,3)/3 to Unity, including ONNX.
        # Demonstrations already contain Unity commands in [-1,1]. The stock
        # BC loss compares those directly with raw Gaussian samples, shrinking
        # executed commands by 3. Convert only the supervised targets here;
        # recordings and all sensor observations retain their physical meaning.
        if self.policy.behavior_spec.action_spec.continuous_size and self.policy.actor.action_model.clip_action:
            expert_actions = expert_actions._replace(continuous_tensor=expert_actions.continuous_tensor * 3)
        mask = getattr(self, '_sample_mask', None)
        if mask is not None and self.policy.behavior_spec.action_spec.discrete_size == 0:
            error = (selected_actions.continuous_tensor - expert_actions.continuous_tensor).square().mean(dim=1)
            return (error * mask).sum() / mask.sum().clamp_min(1)
        return super()._behavioral_cloning_loss(selected_actions, log_probs, expert_actions)
