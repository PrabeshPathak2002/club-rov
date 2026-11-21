import importlib.util
from pathlib import Path
from types import SimpleNamespace
import unittest

import numpy as np
from mlagents.torch_utils import torch
from mlagents_envs.base_env import ActionSpec, ObservationSpec, DimensionProperty, ObservationType
from mlagents.trainers.buffer import AgentBuffer
from mlagents.trainers.settings import NetworkSettings
from mlagents.trainers.trajectory import ObsUtil
from mlagents.trainers.torch_entities.agent_action import AgentAction
from mlagents.trainers.torch_entities.networks import SimpleActor
from sensor_bc import SensorBCModule, repair_legacy_action_head

module_spec = importlib.util.spec_from_file_location('sensor_trainer', Path(__file__).with_name('course-trainer.py'))
trainer = importlib.util.module_from_spec(module_spec)
module_spec.loader.exec_module(trainer)


class SensorTrainingTests(unittest.TestCase):
    def test_sequence_padding_has_no_command_loss_or_gradient(self):
        bc = SensorBCModule.__new__(SensorBCModule)
        bc.policy = SimpleNamespace(behavior_spec=SimpleNamespace(action_spec=ActionSpec.create_continuous(4)),
                                    actor=SimpleNamespace(action_model=SimpleNamespace(clip_action=True)))
        bc._sample_mask = torch.tensor([True, False])
        commands = AgentAction(torch.tensor([[1., 0, 0, 0], [0., 0, 0, 0]]), None)
        values = torch.tensor([[0., 0, 0, 0], [100., 100, 100, 100]], requires_grad=True)
        loss = bc._behavioral_cloning_loss(AgentAction(values, None), None, commands)
        self.assertAlmostEqual(loss.item(), 2.25)
        loss.backward()
        self.assertEqual(values.grad[1].abs().sum().item(), 0)
        self.assertGreater(values.grad[0].abs().sum().item(), 0)

    def test_checkpoint_head_conversion_matches_gain_diagnostic(self):
        actor = SimpleActor([ObservationSpec((16,), (DimensionProperty.NONE,), ObservationType.DEFAULT, 'sensors')],
                            NetworkSettings(hidden_units=32, num_layers=1), ActionSpec.create_continuous(4))
        distribution = actor.action_model._continuous_distribution
        encoding = torch.ones(2, 32)
        with torch.no_grad():
            before = distribution.mu(encoding).clone()
            sigma = distribution.log_sigma.clone()
            repair_legacy_action_head(actor)
            after = distribution.mu(encoding)
        torch.testing.assert_close(after.clamp(-3, 3) / 3, (before.clamp(-3, 3)).clamp(-1, 1))
        torch.testing.assert_close(distribution.log_sigma.exp(), sigma.exp() * 3)

    def test_expert_commands_round_trip_through_ppo_action_scaling(self):
        bc = SensorBCModule.__new__(SensorBCModule)
        bc.policy = SimpleNamespace(behavior_spec=SimpleNamespace(action_spec=ActionSpec.create_continuous(4)),
                                    actor=SimpleNamespace(action_model=SimpleNamespace(clip_action=True)))
        command = torch.tensor([[1., -.5, 0, .25]])
        expert = AgentAction(command, None)
        predicted = AgentAction(command * 3, None)
        self.assertEqual(bc._behavioral_cloning_loss(predicted, None, expert).item(), 0)
        np.testing.assert_allclose(predicted.to_action_tuple(clip=True).continuous, command.cpu().numpy())
        self.assertGreater(bc._behavioral_cloning_loss(expert, None, expert).item(), 0)
        bc.policy.actor.action_model.clip_action = False
        self.assertEqual(bc._behavioral_cloning_loss(expert, None, expert).item(), 0)

    def test_actor_input_statistics_stay_fixed_after_warm_start(self):
        obs = [ObservationSpec((16,), (DimensionProperty.NONE,), ObservationType.DEFAULT, 'sensors')]
        args = dict(observation_specs=obs, network_settings=NetworkSettings(normalize=True, hidden_units=32, num_layers=1),
                    action_spec=ActionSpec.create_continuous(4))
        frozen = trainer.FixedSensorNormalizationActor(**args)
        ordinary = SimpleActor(**args)
        initial, shifted = AgentBuffer(), AgentBuffer()
        for value in (.1, .2, .3):
            initial[ObsUtil.get_name_at(0)].append(np.full(16, value, np.float32))
            shifted[ObsUtil.get_name_at(0)].append(np.full(16, value + .5, np.float32))
        SimpleActor.update_normalization(frozen, initial)
        ordinary.update_normalization(initial)
        before = {name: value.clone() for name, value in frozen.state_dict().items() if 'normalizer' in name}
        frozen.update_normalization(shifted)
        ordinary.update_normalization(shifted)
        self.assertTrue(before)
        for name, value in before.items():
            self.assertTrue(torch.equal(value, frozen.state_dict()[name]))
        self.assertTrue(any(not torch.equal(value, ordinary.state_dict()[name]) for name, value in before.items()))


if __name__ == '__main__':
    unittest.main()
